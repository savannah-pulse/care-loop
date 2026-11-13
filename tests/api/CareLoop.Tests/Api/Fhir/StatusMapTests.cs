using CareLoop.Api.Features.Fhir;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Tracks;
using CareLoop.Tests.Fakes;
using Hl7.Fhir.Model;
using FhirAppointmentStatus = Hl7.Fhir.Model.Appointment.AppointmentStatus;
using FhirTaskStatus = Hl7.Fhir.Model.Task.TaskStatus;

namespace CareLoop.Tests.Api.Fhir;

/// <summary>
/// Every CareLoop to FHIR code translation lives in StatusMap.
/// </summary>
public sealed class StatusMapTests
{
    private static readonly DateTimeOffset At = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(CaseStage.Received, "received")]
    [InlineData(CaseStage.ReviewPending, "review-pending")]
    [InlineData(CaseStage.FollowUpActive, "follow-up-active")]
    [InlineData(CaseStage.ClosureReview, "closure-review")]
    [InlineData(CaseStage.Closed, "closed")]
    public void Stage_maps_to_business_status(CaseStage stage, string code)
    {
        var concept = StatusMap.ToBusinessStatus(stage);

        var coding = Assert.Single(concept.Coding);
        Assert.Equal(StatusMap.StageSystem, coding.System);
        Assert.Equal(code, coding.Code);
        Assert.Equal(stage, StatusMap.ToStage(concept));
    }

    [Fact]
    public void Closure_reason_round_trips_through_status_reason()
    {
        foreach (var reason in Enum.GetValues<ClosureReason>())
        {
            var concept = StatusMap.ToStatusReason(reason);

            Assert.Equal(StatusMap.StatusReasonSystem, concept.Coding[0].System);
            Assert.Equal(reason, StatusMap.ToClosureReason(concept));
        }
    }

    [Fact]
    public void Pending_handoff_maps_to_task_status_requested()
    {
        var @case = TestData.NewCase(At);
        @case.ProposeOwner(new Assignment
        {
            ProposedOwner = TestData.Clinician("SYN-STAFF-0002"),
            ProposedBy = Envelopes.Clinician(),
            ProposedAt = At,
            RespondBy = At.AddHours(4),
            Status = AssignmentStatus.Proposed,
        });

        Assert.Equal(FhirTaskStatus.Requested, StatusMap.ToTaskStatus(@case));
    }

    [Fact]
    public void Owned_case_maps_to_accepted_then_in_progress()
    {
        var @case = TestData.NewCase(At);
        Assert.Equal(FhirTaskStatus.Accepted, StatusMap.ToTaskStatus(@case));

        @case.MoveTo(CaseStage.ReviewPending, At);
        Assert.Equal(FhirTaskStatus.InProgress, StatusMap.ToTaskStatus(@case));
    }

    [Fact]
    public async System.Threading.Tasks.Task Closed_case_maps_to_task_status_completed()
    {
        var harness = new IngestionHarness();
        await harness.CaseInClosureReviewAsync();
        var closed = await harness.ApplyAsync(Envelopes.Closure(harness.Clock.UtcNow));

        Assert.Equal(CaseStage.Closed, closed.Stage);
        Assert.Equal(FhirTaskStatus.Completed, StatusMap.ToTaskStatus(closed));
    }

    [Fact]
    public void Track_statuses_map_as_documented()
    {
        Assert.Equal(ConsultationStatus.Requested, StatusMap.ToConsultationStatus(FhirTaskStatus.Requested));
        Assert.Equal(ConsultationStatus.Accepted, StatusMap.ToConsultationStatus(FhirTaskStatus.Accepted));
        Assert.Equal(ConsultationStatus.Rejected, StatusMap.ToConsultationStatus(FhirTaskStatus.Rejected));
        Assert.Equal(ConsultationStatus.OutcomeRecorded, StatusMap.ToConsultationStatus(FhirTaskStatus.Completed));

        Assert.Equal(ReferralStatus.Sent, StatusMap.ToReferralStatus(FhirTaskStatus.Requested));
        Assert.Equal(ReferralStatus.Accepted, StatusMap.ToReferralStatus(FhirTaskStatus.Accepted));
        Assert.Equal(ReferralStatus.Rejected, StatusMap.ToReferralStatus(FhirTaskStatus.Rejected));
        Assert.Null(StatusMap.ToReferralStatus(FhirTaskStatus.Completed));

        Assert.Equal(AppointmentStatus.Scheduled, StatusMap.ToAppointmentStatus(FhirAppointmentStatus.Booked));
        Assert.Equal(AppointmentStatus.Attended, StatusMap.ToAppointmentStatus(FhirAppointmentStatus.Fulfilled));
        Assert.Equal(AppointmentStatus.Missed, StatusMap.ToAppointmentStatus(FhirAppointmentStatus.Noshow));
    }

    [Theory]
    [InlineData(StatusMap.DeliveryStatusCategory, EventStatus.Completed, NotificationUpdateKind.Delivered)]
    [InlineData(StatusMap.DeliveryStatusCategory, EventStatus.NotDone, NotificationUpdateKind.Failed)]
    [InlineData(StatusMap.AcknowledgementCategory, EventStatus.Completed, NotificationUpdateKind.Acknowledged)]
    [InlineData(StatusMap.TeachBackCategory, EventStatus.Completed, NotificationUpdateKind.Understood)]
    [InlineData(StatusMap.CallMeCategory, EventStatus.Completed, NotificationUpdateKind.CallRequested)]
    [InlineData(StatusMap.ConfidenceRatingCategory, EventStatus.Completed, NotificationUpdateKind.ConfidenceRated)]
    public void Communication_category_and_status_map_to_a_notification_update(string category, EventStatus status, NotificationUpdateKind expected)
    {
        var communication = Communication(category, status);

        Assert.Equal(expected, StatusMap.ToNotificationUpdateKind(communication));
    }

    [Fact]
    public void A_delivery_report_is_never_an_acknowledgement()
    {
        foreach (var status in Enum.GetValues<EventStatus>())
        {
            var kind = StatusMap.ToNotificationUpdateKind(Communication(StatusMap.DeliveryStatusCategory, status));
            Assert.NotEqual(NotificationUpdateKind.Acknowledged, kind);
        }
    }

    [Fact]
    public void Confidence_rating_carries_its_value()
    {
        var communication = Communication(StatusMap.ConfidenceRatingCategory, EventStatus.Completed);
        communication.Payload.Add(new Communication.PayloadComponent { Content = new FhirString("4") });

        Assert.Equal(4, StatusMap.ToConfidence(communication));
        Assert.Null(StatusMap.ToConfidence(Communication(StatusMap.ConfidenceRatingCategory, EventStatus.Completed)));
    }

    [Fact]
    public void Unknown_codes_map_to_null()
    {
        var foreign = new CodeableConcept("https://example.invalid/other", "treated");

        Assert.Null(StatusMap.ToClosureReason(foreign));
        Assert.Null(StatusMap.ToClosureReason(new CodeableConcept(StatusMap.StatusReasonSystem, "no-such-reason")));
        Assert.Null(StatusMap.ToClosureReason(null));
        Assert.Null(StatusMap.ToStage(foreign));
        Assert.Null(StatusMap.ToEvidenceKind(foreign));
        Assert.Null(StatusMap.ToConsultationStatus(FhirTaskStatus.Draft));
        Assert.Null(StatusMap.ToReferralStatus(null));
        Assert.Null(StatusMap.ToAppointmentStatus(FhirAppointmentStatus.Cancelled));
        Assert.Null(StatusMap.ToNotificationUpdateKind(Communication("no-such-category", EventStatus.Completed)));
        Assert.Null(StatusMap.ToNotificationUpdateKind(Communication(StatusMap.AcknowledgementCategory, EventStatus.InProgress)));
    }

    private static Communication Communication(string category, EventStatus status) => new()
    {
        Id = "SYN-COMM-0001",
        Status = status,
        Category = [new CodeableConcept(StatusMap.CommunicationCategorySystem, category)],
    };
}

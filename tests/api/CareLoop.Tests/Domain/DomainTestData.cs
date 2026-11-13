using CareLoop.Api.Infrastructure.Pathways;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Tracks;
using CareLoop.Tests.Fakes;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CareLoop.Tests.Domain;

/// <summary>
/// Builders for the Domain tests: the real placeholder pathway, actors, closure decisions and cases
/// moved to a given stage through the aggregate's own methods. Every identifier is synthetic.
/// </summary>
internal static class DomainTestData
{
    private static readonly Lazy<PathwayDefinition> BreastScreeningPathway = new(() => Provider().Get("breast-screening"));

    /// <summary>The placeholder breast-screening pathway shipped with the API, loaded once per test run.</summary>
    public static PathwayDefinition Pathway => BreastScreeningPathway.Value;

    /// <summary>A provider reading the pathway folder copied next to the test assembly.</summary>
    public static JsonPathwayProvider Provider(string? directory = null) =>
        new(
            new TestHostEnvironment(AppContext.BaseDirectory),
            Options.Create(new PathwayOptions { Directory = directory ?? "Infrastructure/Pathways" }));

    /// <summary>A synthetic clinician actor.</summary>
    public static Actor ClinicianActor(string id = "SYN-STAFF-0001") =>
        new() { Reference = id, Role = "clinician", FacilityId = "SYN-FAC-A" };

    /// <summary>A synthetic actor in a role the placeholder pathway never authorizes.</summary>
    public static Actor ReceptionistActor(string id = "SYN-STAFF-0099") =>
        new() { Reference = id, Role = "receptionist", FacilityId = "SYN-FAC-A" };

    /// <summary>The actor CareLoop's own automation acts as.</summary>
    public static Actor SystemActor() =>
        new() { Reference = "careloop", Role = "system" };

    /// <summary>The owner record matching <see cref="ClinicianActor"/> with the same identifier.</summary>
    public static CaseOwner ClinicianOwner(string id = "SYN-STAFF-0002") =>
        TestData.Clinician(id);

    /// <summary>A case opened by a clinician and moved to review pending.</summary>
    public static Case InReviewPending(FakeClock clock, CaseOwner? owner = null)
    {
        var @case = TestData.NewCase(clock.UtcNow, owner: owner);
        @case.MoveTo(CaseStage.ReviewPending, clock.UtcNow);
        @case.SetNextAction("review-result", clock.UtcNow.AddDays(1), clock.UtcNow);
        return @case;
    }

    /// <summary>A reviewed case in follow-up with a requested notification and a next action.</summary>
    public static Case InFollowUp(FakeClock clock, CaseOwner? owner = null)
    {
        var @case = InReviewPending(clock, owner);
        @case.RecordReview("PLACEHOLDER-URGENCY-A", ClinicianActor(), clock.UtcNow);
        @case.MoveTo(CaseStage.FollowUpActive, clock.UtcNow);
        @case.BeginNotification("result-notification", clock.UtcNow);
        @case.SetNextAction("acknowledge-notification", clock.UtcNow.AddDays(1), clock.UtcNow);
        @case.ClearDomainEvents();
        return @case;
    }

    /// <summary>
    /// A case that has everything the placeholder pathway asks of completed care: acknowledged
    /// notification, attended appointment, recorded outcome, and the closure-review stage.
    /// </summary>
    public static Case ReadyForClosure(FakeClock clock, string evidenceReference = "SYN-PROC-0001")
    {
        var @case = InFollowUp(clock);
        @case.Notification!.Advance(NotificationStatus.Acknowledged, clock.UtcNow, "SYN-COMM-0001");
        @case.BeginAppointment("SYN-APPT-0001", clock.UtcNow.AddDays(2), clock.UtcNow);
        @case.Appointment!.Advance(AppointmentStatus.Attended, clock.UtcNow, "SYN-APPT-0001");
        @case.RecordOutcomeEvidence(evidenceReference, clock.UtcNow);
        @case.MoveTo(CaseStage.ClosureReview, clock.UtcNow);
        @case.SetNextAction("decide-closure", clock.UtcNow.AddDays(1), clock.UtcNow);
        @case.ClearDomainEvents();
        return @case;
    }

    /// <summary>A closed case.</summary>
    public static Case Closed(FakeClock clock)
    {
        var @case = ReadyForClosure(clock);
        @case.Close(TreatedDecision(clock), clock.UtcNow);
        @case.ClearDomainEvents();
        return @case;
    }

    /// <summary>A valid closure decision for reason Treated, by a clinician, with evidence.</summary>
    public static ClosureDecision TreatedDecision(FakeClock clock, string evidenceReference = "SYN-PROC-0001", Actor? authorizedBy = null) =>
        new()
        {
            Reason = ClosureReason.Treated,
            AuthorizedBy = authorizedBy ?? ClinicianActor(),
            DecidedAt = clock.UtcNow,
            EvidenceReference = evidenceReference,
        };

    /// <summary>An <see cref="IHostEnvironment"/> whose content root is a folder the test chooses.</summary>
    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
        }

        public string EnvironmentName { get; set; } = "Test";

        public string ApplicationName { get; set; } = "CareLoop.Tests";

        public string ContentRootPath { get; set; }

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

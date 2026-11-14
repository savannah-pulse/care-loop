using CareLoop.Api.Features.Notifications;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events.DomainEvents;
using CareLoop.Domain.Recipients;
using CareLoop.Domain.Tracks;
using CareLoop.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using static CareLoop.Tests.Api.Notifications.NotificationTestSupport;

namespace CareLoop.Tests.Api.Notifications;

/// <summary>
/// The routing table, staff privacy and idempotency of direct notifications.
/// </summary>
public sealed class NotificationRouterTests : IDisposable
{
    private readonly NotificationStore _store = new();
    private readonly FakeProvider _simulator = new(NotificationChannel.Simulator);
    private readonly NotificationSendLog _log = new();

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task NotificationRequested_goes_to_the_patient()
    {
        var requested = await _store.ReviewedCaseAsync();

        await RouteAsync(requested);

        var message = Assert.Single(_simulator.Sent);
        Assert.Equal(RecipientKind.Patient, message.Recipient.Kind);
        Assert.Equal("SYN-PT-0001", message.Recipient.Reference);
        Assert.Equal("SYN-INBOX-0001", message.ContactPoint.Address);
        Assert.Equal(requested.TemplateKey, message.TemplateKey);
        Assert.Contains(NotificationRouter.CaseReferenceFor(requested.CaseId), message.Body);
        Assert.Equal(NotificationSender.AttemptIdFor(requested.EventId, "SYN-PT-0001", NotificationChannel.Simulator), message.AttemptId);
    }

    [Fact]
    public async Task HandoffProposed_goes_to_the_proposed_owner()
    {
        var @case = await _store.OpenCaseAsync();
        var proposed = TestData.Clinician("SYN-STAFF-0002");

        await RouteAsync(new HandoffProposed { EventId = Guid.NewGuid(), CaseId = @case.Id, OccurredAt = _store.Clock.UtcNow, CurrentOwner = @case.Owner, ProposedOwner = proposed, RespondBy = _store.Clock.UtcNow.AddHours(4) });

        AssertStaffNotice("SYN-STAFF-0002");
    }

    [Fact]
    public async Task HandoffRejected_goes_to_the_current_owner()
    {
        var @case = await _store.OpenCaseAsync();

        await RouteAsync(new HandoffRejected { EventId = Guid.NewGuid(), CaseId = @case.Id, OccurredAt = _store.Clock.UtcNow, CurrentOwner = @case.Owner, RejectedBy = TestData.Clinician("SYN-STAFF-0002"), Reason = "PLACEHOLDER: no capacity" });

        AssertStaffNotice(@case.Owner.Reference);
    }

    [Fact]
    public async Task EscalationRaised_goes_to_the_facility_lead_then_the_coordinator()
    {
        var @case = await _store.OpenCaseAsync();
        var coordinator = new CaseOwner { Reference = "SYN-QUEUE-COORD", Role = "coordinator", FacilityId = "SYN-FAC-A", IsQueue = true };

        await RouteAsync(Escalation(@case, 1, TestData.FacilityLeadQueue()));
        await RouteAsync(Escalation(@case, 2, coordinator));

        Assert.Equal(["SYN-QUEUE-FAC-A", "SYN-QUEUE-COORD"], _simulator.Sent.Select(m => m.ContactPoint.Address));
        Assert.All(_simulator.Sent, m => Assert.Equal(RecipientKind.Staff, m.Recipient.Kind));
    }

    [Fact]
    public async Task CaseClosed_notifies_nobody()
    {
        var @case = await _store.OpenCaseAsync();

        await RouteAsync(new CaseClosed { EventId = Guid.NewGuid(), CaseId = @case.Id, OccurredAt = _store.Clock.UtcNow, Reason = ClosureReason.Treated, AuthorizedBy = Envelopes.Clinician() });
        await RouteAsync(new HandoffSummaryApproved { EventId = Guid.NewGuid(), CaseId = @case.Id, OccurredAt = _store.Clock.UtcNow, SummaryId = Guid.NewGuid(), ApprovedBy = Envelopes.Clinician() });

        Assert.Empty(_simulator.Sent);
    }

    [Fact]
    public async Task Staff_notification_carries_no_patient_detail()
    {
        await _store.IngestAsync(Envelopes.ScreeningResult(_store.Clock.UtcNow, namedOwner: Envelopes.Clinician(), patientDisplayName: "SYNTHETIC Patient One", patientSecondaryIdentifier: "SYN-MRN-0001"));
        var @case = await _store.TheCaseAsync();

        await RouteAsync(new DeadlineApproaching { EventId = Guid.NewGuid(), CaseId = @case.Id, OccurredAt = _store.Clock.UtcNow, CurrentOwner = @case.Owner, NextAction = "review-result", DueAt = _store.Clock.UtcNow.AddHours(1) });
        await RouteAsync(new ReferralRejected { EventId = Guid.NewGuid(), CaseId = @case.Id, OccurredAt = _store.Clock.UtcNow, CurrentOwner = @case.Owner, Reason = "PLACEHOLDER: out of area" });
        await RouteAsync(new CallRequested { EventId = Guid.NewGuid(), CaseId = @case.Id, OccurredAt = _store.Clock.UtcNow, CurrentOwner = @case.Owner, DueAt = _store.Clock.UtcNow.AddHours(2) });

        Assert.Equal(3, _simulator.Sent.Count);
        foreach (var message in _simulator.Sent)
        {
            Assert.Equal(NotificationRouter.StaffTemplateKey, message.TemplateKey);
            var text = $"{message.Subject} {message.Body}";
            Assert.Contains(NotificationRouter.HisLinkFor(@case.Id), text);
            foreach (var patientDetail in new[] { "SYN-PT-0001", "SYNTHETIC Patient One", "SYN-MRN-0001", "SYN-INBOX-0001", Envelopes.ResultId, "out of area" })
            {
                Assert.DoesNotContain(patientDetail, text);
            }
        }
    }

    [Fact]
    public async Task Handling_the_same_event_twice_sends_once()
    {
        var requested = await _store.ReviewedCaseAsync();

        await RouteAsync(requested);
        await RouteAsync(requested);

        Assert.Single(_simulator.Sent);
    }

    [Fact]
    public async Task Notifying_does_not_change_owner_or_deadline()
    {
        var requested = await _store.ReviewedCaseAsync();
        var before = await _store.TheCaseAsync();

        await RouteAsync(requested);
        await RouteAsync(new DeadlineApproaching { EventId = Guid.NewGuid(), CaseId = before.Id, OccurredAt = _store.Clock.UtcNow, CurrentOwner = before.Owner, NextAction = before.NextAction!, DueAt = before.DueAt!.Value });

        var after = await _store.TheCaseAsync();
        Assert.Equal(2, _simulator.Sent.Count);
        Assert.Equal(before.Owner, after.Owner);
        Assert.Equal(before.DueAt, after.DueAt);
        Assert.Equal(before.NextAction, after.NextAction);
        Assert.Equal(before.Notification!.Status, after.Notification!.Status);
        Assert.Equal(before.Version, after.Version);
    }

    [Fact]
    public async Task Refused_patient_send_is_recorded_on_the_case_as_a_failed_attempt()
    {
        _simulator.Answer = _ => new SendResult { Accepted = false, FailureReason = "PLACEHOLDER: inbox unavailable" };
        var requested = await _store.ReviewedCaseAsync();

        await RouteAsync(requested);

        var @case = await _store.TheCaseAsync();
        var attempt = Assert.Single(@case.Notification!.Attempts);
        Assert.Equal(_simulator.Sent[0].AttemptId, attempt.Id);
        Assert.Equal(NotificationAttemptStatus.Failed, attempt.Status);
        Assert.Equal(NotificationStatus.Failed, @case.Notification.Status);
        Assert.True(@case.Exceptions.HasFlag(CaseExceptions.Unreachable));
    }

    [Fact]
    public async Task Patient_no_channel_can_reach_is_recorded_as_unreachable()
    {
        _simulator.Reach = _ => false;
        var requested = await _store.ReviewedCaseAsync();

        await RouteAsync(requested);

        Assert.Empty(_simulator.Sent);
        var @case = await _store.TheCaseAsync();
        Assert.Equal(NotificationRouter.NoChannelReason, Assert.Single(@case.Notification!.Attempts).FailureReason);
        Assert.True(@case.Exceptions.HasFlag(CaseExceptions.Unreachable));
    }

    private EscalationRaised Escalation(Case @case, int level, CaseOwner to) =>
        new() { EventId = Guid.NewGuid(), CaseId = @case.Id, OccurredAt = _store.Clock.UtcNow, Level = level, EscalatedTo = to, CurrentOwner = @case.Owner, Reason = "PLACEHOLDER: review-result overdue" };

    private void AssertStaffNotice(string address)
    {
        var message = Assert.Single(_simulator.Sent);
        Assert.Equal(RecipientKind.Staff, message.Recipient.Kind);
        Assert.Equal(address, message.ContactPoint.Address);
        Assert.Equal(NotificationRouter.StaffTemplateKey, message.TemplateKey);
    }

    private async Task RouteAsync(IDomainEvent domainEvent)
    {
        await using var db = _store.NewContext();
        var router = new NotificationRouter(
            new RecipientResolver(db),
            Sender(_store.Clock, Enabled("Simulator"), _simulator),
            _log,
            _store.Ingestor(db),
            db,
            NullLogger<NotificationRouter>.Instance);
        await router.HandleAsync(domainEvent, CancellationToken.None);
    }
}

using CareLoop.Api.Features.Notifications;
using CareLoop.Contracts.Events.Payloads;
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
    private readonly FakeProvider _email = new(NotificationChannel.Email);
    private readonly NotificationSendLog _log = new();

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task NotificationRequested_goes_to_the_patient()
    {
        var requested = await _store.ReviewedCaseAsync();

        await RouteAsync(requested);

        var message = Assert.Single(_email.Sent);
        Assert.Equal(RecipientKind.Patient, message.Recipient.Kind);
        Assert.Equal("SYN-PT-0001", message.Recipient.Reference);
        Assert.Equal(Envelopes.PatientEmail, message.ContactPoint.Address);
        Assert.Equal(requested.TemplateKey, message.TemplateKey);
        Assert.Contains(NotificationRouter.CaseReferenceFor(requested.CaseId), message.Body);
        Assert.Equal(NotificationSender.AttemptIdFor(requested.EventId, "SYN-PT-0001", NotificationChannel.Email), message.AttemptId);
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

        Assert.Equal(["SYN-QUEUE-FAC-A", "SYN-QUEUE-COORD"], _email.Sent.Select(m => m.Recipient.Reference));
        Assert.All(_email.Sent, m => Assert.Equal(NotificationTestSupport.StaffAddress, m.ContactPoint.Address));
        Assert.All(_email.Sent, m => Assert.Equal(RecipientKind.Staff, m.Recipient.Kind));
    }

    [Fact]
    public async Task CaseClosed_notifies_nobody()
    {
        var @case = await _store.OpenCaseAsync();

        await RouteAsync(new CaseClosed { EventId = Guid.NewGuid(), CaseId = @case.Id, OccurredAt = _store.Clock.UtcNow, Reason = ClosureReason.Treated, AuthorizedBy = Envelopes.Clinician() });
        await RouteAsync(new HandoffSummaryApproved { EventId = Guid.NewGuid(), CaseId = @case.Id, OccurredAt = _store.Clock.UtcNow, SummaryId = Guid.NewGuid(), ApprovedBy = Envelopes.Clinician() });

        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task Staff_notification_carries_no_patient_detail()
    {
        await _store.IngestAsync(Envelopes.ScreeningResult(_store.Clock.UtcNow, namedOwner: Envelopes.Clinician(), patientDisplayName: "SYNTHETIC Patient One", patientSecondaryIdentifier: "SYN-MRN-0001"));
        var @case = await _store.TheCaseAsync();

        await RouteAsync(new DeadlineApproaching { EventId = Guid.NewGuid(), CaseId = @case.Id, OccurredAt = _store.Clock.UtcNow, CurrentOwner = @case.Owner, NextAction = "review-result", DueAt = _store.Clock.UtcNow.AddHours(1) });
        await RouteAsync(new ReferralRejected { EventId = Guid.NewGuid(), CaseId = @case.Id, OccurredAt = _store.Clock.UtcNow, CurrentOwner = @case.Owner, Reason = "PLACEHOLDER: out of area" });
        await RouteAsync(new CallRequested { EventId = Guid.NewGuid(), CaseId = @case.Id, OccurredAt = _store.Clock.UtcNow, CurrentOwner = @case.Owner, DueAt = _store.Clock.UtcNow.AddHours(2) });

        Assert.Equal(3, _email.Sent.Count);
        foreach (var message in _email.Sent)
        {
            Assert.Equal(NotificationRouter.StaffTemplateKey, message.TemplateKey);
            var text = $"{message.Subject} {message.Body}";
            Assert.Contains(NotificationRouter.CaseReferenceFor(@case.Id), text);
            Assert.DoesNotContain("Task/", text);
            foreach (var patientDetail in new[] { "SYN-PT-0001", "SYNTHETIC Patient One", "SYN-MRN-0001", Envelopes.PatientPhone, Envelopes.PatientEmail, Envelopes.ResultId, "out of area" })
            {
                Assert.DoesNotContain(patientDetail, text);
            }
        }
    }

    [Fact]
    public async Task A_call_back_is_sent_to_the_role_that_makes_the_call_not_the_owner()
    {
        var @case = await _store.OpenCaseAsync();

        await RouteAsync(new CallRequested
        {
            EventId = Guid.NewGuid(), CaseId = @case.Id, OccurredAt = _store.Clock.UtcNow, CurrentOwner = TestData.FacilityLeadQueue(),
            DueAt = _store.Clock.UtcNow.AddHours(2), CallerRole = "nurse-navigator",
        });
        await RouteAsync(new CallRequested
        {
            EventId = Guid.NewGuid(), CaseId = @case.Id, OccurredAt = _store.Clock.UtcNow, CurrentOwner = TestData.FacilityLeadQueue(),
            DueAt = _store.Clock.UtcNow.AddHours(2),
        });

        Assert.Equal(2, _email.Sent.Count);
        Assert.Equal("nurse-navigator", _email.Sent[0].Recipient.Reference);
        Assert.Equal(TestData.FacilityLeadQueue().Reference, _email.Sent[1].Recipient.Reference);
    }

    [Fact]
    public async Task Staff_notice_names_the_facility_and_points_to_CareLoop()
    {
        var @case = await _store.OpenCaseAsync();
        var format = new MessageFormat(new NotificationOptions { FacilityNames = new(StringComparer.OrdinalIgnoreCase) { [@case.FacilityId] = "SYN Family Health Centre" } });

        await RouteAsync(new DeadlineApproaching { EventId = Guid.NewGuid(), CaseId = @case.Id, OccurredAt = _store.Clock.UtcNow, CurrentOwner = @case.Owner, NextAction = "review-result", DueAt = _store.Clock.UtcNow.AddHours(1) }, format: format);

        var body = Assert.Single(_email.Sent).Body;
        Assert.Contains("A case at SYN Family Health Centre needs your action. Open CareLoop and look for case " + NotificationRouter.CaseReferenceFor(@case.Id) + ".", body);
        Assert.DoesNotContain("HIS", body);
    }

    [Fact]
    public async Task Handling_the_same_event_twice_sends_once()
    {
        var requested = await _store.ReviewedCaseAsync();

        await RouteAsync(requested);
        await RouteAsync(requested);

        Assert.Single(_email.Sent);
    }

    [Fact]
    public async Task Notifying_does_not_change_owner_or_deadline()
    {
        var requested = await _store.ReviewedCaseAsync();
        var before = await _store.TheCaseAsync();

        await RouteAsync(requested);
        await RouteAsync(new DeadlineApproaching { EventId = Guid.NewGuid(), CaseId = before.Id, OccurredAt = _store.Clock.UtcNow, CurrentOwner = before.Owner, NextAction = before.NextAction!, DueAt = before.DueAt!.Value });

        var after = await _store.TheCaseAsync();
        Assert.Equal(2, _email.Sent.Count);
        Assert.Equal(before.Owner, after.Owner);
        Assert.Equal(before.DueAt, after.DueAt);
        Assert.Equal(before.NextAction, after.NextAction);
        Assert.Equal(before.Notification!.Status, after.Notification!.Status);
        // The one thing written is the send itself, recorded on the track before it went out.
        var sent = Assert.Single(after.Notification.Attempts);
        Assert.Equal(NotificationAttemptStatus.Sent, sent.Status);
        Assert.Equal(_email.Sent[0].AttemptId, sent.Id);
    }

    [Fact]
    public async Task Refused_patient_send_is_recorded_on_the_case_as_a_failed_attempt()
    {
        _email.Answer = _ => new SendResult { Accepted = false, FailureReason = "PLACEHOLDER: inbox unavailable" };
        var requested = await _store.ReviewedCaseAsync();

        await RouteAsync(requested);

        var @case = await _store.TheCaseAsync();
        var attempt = Assert.Single(@case.Notification!.Attempts);
        Assert.Equal(_email.Sent[0].AttemptId, attempt.Id);
        Assert.Equal(NotificationAttemptStatus.Failed, attempt.Status);
        Assert.Equal(NotificationStatus.Failed, @case.Notification.Status);
        Assert.True(@case.Exceptions.HasFlag(CaseExceptions.Unreachable));
    }

    [Fact]
    public async Task Patient_no_channel_can_reach_is_recorded_as_unreachable()
    {
        _email.Reach = _ => false;
        var requested = await _store.ReviewedCaseAsync();

        await RouteAsync(requested);

        Assert.Empty(_email.Sent);
        var @case = await _store.TheCaseAsync();
        Assert.Equal(NotificationRouter.NoChannelReason, Assert.Single(@case.Notification!.Attempts).FailureReason);
        Assert.True(@case.Exceptions.HasFlag(CaseExceptions.Unreachable));
    }

    [Fact]
    public async Task AppointmentScheduled_goes_to_a_consented_caregiver_with_date_time_and_place_only()
    {
        var scheduled = await BookedWithConsentAsync("SYN Breast Imaging Clinic");

        await RouteAsync(scheduled);

        var message = Assert.Single(_email.Sent, m => m.Recipient.Kind == RecipientKind.Caregiver);
        Assert.Equal(CaregiverInbox, message.ContactPoint.Address);
        Assert.Equal(NotificationRouter.CaregiverTemplateKey, message.TemplateKey);
        Assert.Contains(MessageFormat.Default.Date(scheduled.ScheduledFor), message.Body);
        Assert.Contains(MessageFormat.Default.Time(scheduled.ScheduledFor), message.Body);
        Assert.Contains("SYN Breast Imaging Clinic", message.Body);
        var text = $"{message.Subject} {message.Body}";
        foreach (var patientDetail in new[] { "SYN-PT-0001", Envelopes.PatientPhone, Envelopes.PatientEmail, Envelopes.ResultId, NotificationRouter.CaseReferenceFor(scheduled.CaseId), "PLACEHOLDER-URGENCY-A" })
        {
            Assert.DoesNotContain(patientDetail, text);
        }
    }

    [Fact]
    public async Task AppointmentScheduled_without_a_place_says_it_will_be_confirmed()
    {
        var scheduled = await BookedWithConsentAsync(location: null);

        await RouteAsync(scheduled);

        var caregiver = Assert.Single(_email.Sent, m => m.Recipient.Kind == RecipientKind.Caregiver);
        Assert.Equal(NotificationRouter.CaregiverPlaceToBeConfirmedTemplateKey, caregiver.TemplateKey);
        Assert.Contains("to be confirmed", caregiver.Body);
        var patient = Assert.Single(_email.Sent, m => m.Recipient.Kind == RecipientKind.Patient);
        Assert.Equal(NotificationRouter.PatientPlaceToBeConfirmedTemplateKey, patient.TemplateKey);
        Assert.Contains("to be confirmed", patient.Body);
    }

    [Fact]
    public async Task AppointmentScheduled_tells_the_patient_the_day_time_and_place_in_local_time_with_the_reply_codes()
    {
        await _store.ReviewedCaseAsync();
        var at = new DateTimeOffset(2026, 11, 24, 9, 30, 0, TimeSpan.Zero);
        await _store.IngestAsync(Envelopes.Appointment(_store.Clock.UtcNow, AppointmentStatus.Scheduled, scheduledFor: at, location: "SYN Breast Imaging Clinic"));
        var scheduled = Assert.Single(await _store.RaisedAsync<AppointmentScheduled>());
        var before = await _store.TheCaseAsync();

        await RouteAsync(scheduled, format: new MessageFormat(new NotificationOptions { TimeZone = "Asia/Dubai" }));

        var message = Assert.Single(_email.Sent);
        Assert.Equal(RecipientKind.Patient, message.Recipient.Kind);
        Assert.Equal(NotificationRouter.PatientAppointmentTemplateKey, message.TemplateKey);
        Assert.Contains("Tuesday 24 November 2026", message.Body);
        Assert.Contains("13:30", message.Body);
        Assert.Contains("SYN Breast Imaging Clinic", message.Body);
        Assert.Contains("Reply 1 to confirm you got this message", message.Body);
        Assert.Contains(NotificationRouter.CaseReferenceFor(scheduled.CaseId), message.Body);
        // Not the follow-up notification: the track is left as it was.
        Assert.Equal(before.Notification!.Attempts.Count, (await _store.TheCaseAsync()).Notification!.Attempts.Count);
    }

    [Fact]
    public async Task AppointmentScheduled_without_consent_goes_to_the_patient_only()
    {
        await _store.ReviewedCaseAsync();
        await _store.IngestAsync(Envelopes.Appointment(_store.Clock.UtcNow, AppointmentStatus.Scheduled, scheduledFor: _store.Clock.UtcNow.AddDays(3), location: "SYN Breast Imaging Clinic"));

        await RouteAsync(Assert.Single(await _store.RaisedAsync<AppointmentScheduled>()));

        Assert.Equal(RecipientKind.Patient, Assert.Single(_email.Sent).Recipient.Kind);
    }

    [Fact]
    public async Task Consent_withdrawn_after_the_booking_stops_the_caregiver_message()
    {
        var scheduled = await BookedWithConsentAsync("SYN Breast Imaging Clinic");

        Assert.Equal(
            CareLoop.Api.Features.Events.IngestOutcome.Applied,
            (await _store.IngestAsync(Envelopes.Consent(_store.Clock.UtcNow, ConsentUpdateKind.Withdrawn, consentReference: "Consent/SYN-CONSENT-0001/_history/2"))).Outcome);

        Assert.False((await _store.TheCaseAsync()).CaregiverConsent!.IsActive);
        await RouteAsync(scheduled);

        Assert.DoesNotContain(_email.Sent, m => m.Recipient.Kind == RecipientKind.Caregiver);
    }

    [Fact]
    public async Task Caregiver_sms_says_the_same_as_the_email_and_each_send_is_its_own_attempt()
    {
        var sms = new FakeProvider(NotificationChannel.Sms, allowsSensitiveContent: false);
        var scheduled = await BookedWithConsentAsync("SYN Breast Imaging Clinic", NotificationChannel.Sms);

        await using var db = _store.NewContext();
        var recipient = Assert.Single(await NotificationTestSupport.Resolver(db, NotificationChannel.Email).ResolveAsync(scheduled, CancellationToken.None), r => r.Kind == RecipientKind.Caregiver);
        var both = recipient with { ContactPoints = [.. recipient.ContactPoints, new ContactPoint { Channel = NotificationChannel.Email, Address = CaregiverInbox }] };
        var attempts = await Sender(_store.Clock, Enabled("Email", "Sms"), _email, sms).SendAsync(
            scheduled.CaseId, both, NotificationRouter.CaregiverTemplateKey, nameof(AppointmentScheduled),
            new Dictionary<string, string> { ["appointmentDate"] = "2026-11-17", ["appointmentTime"] = "09:30", ["place"] = "SYN Breast Imaging Clinic" },
            CancellationToken.None, scheduled.EventId);

        Assert.Equal(2, attempts.Select(a => a.Id).Distinct().Count());
        var text = Assert.Single(sms.Sent);
        Assert.True(text.IsSmsSafe);
        Assert.Contains("2026-11-17", text.Body);
        Assert.Null(text.Subject);
        Assert.Contains("09:30", text.Body);
        Assert.Contains("SYN Breast Imaging Clinic", text.Body);
        Assert.Contains("SYN Breast Imaging Clinic", Assert.Single(_email.Sent).Body);
    }

    [Fact]
    public async Task Escalation_notice_says_who_acknowledged_the_previous_level()
    {
        var @case = await _store.OpenCaseAsync();
        var coordinator = new CaseOwner { Reference = "SYN-QUEUE-COORD", Role = "coordinator", FacilityId = "SYN-FAC-A", IsQueue = true };
        var acknowledgedAt = new DateTimeOffset(2026, 11, 14, 9, 40, 0, TimeSpan.Zero);

        var dubai = new MessageFormat(new NotificationOptions { TimeZone = "Asia/Dubai" });
        await RouteAsync(Escalation(@case, 1, TestData.FacilityLeadQueue()), format: dubai);
        await RouteAsync(Escalation(@case, 2, coordinator) with { AcknowledgedByRole = "facility-lead", AcknowledgedAt = acknowledgedAt }, format: dubai);

        Assert.Equal(2, _email.Sent.Count);
        Assert.DoesNotContain("acknowledged", _email.Sent[0].Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("The facility lead acknowledged level 1 at 13:40 on Saturday 14 November.", _email.Sent[1].Body);
    }

    [Fact]
    public async Task Refused_staff_notice_is_retried_until_sent_and_then_not_sent_again()
    {
        var @case = await _store.OpenCaseAsync();
        var approaching = new DeadlineApproaching { EventId = Guid.NewGuid(), CaseId = @case.Id, OccurredAt = _store.Clock.UtcNow, CurrentOwner = @case.Owner, NextAction = "review-result", DueAt = _store.Clock.UtcNow.AddHours(1) };
        _email.Answer = _ => new SendResult { Accepted = false, FailureReason = "The email simulator answered 503 Service Unavailable." };

        var refused = await Assert.ThrowsAsync<NotificationNotSentException>(() => RouteAsync(approaching));
        Assert.Contains("503", refused.Message);
        Assert.DoesNotContain("SYN-PT-0001", refused.Message);

        _email.Answer = m => new SendResult { Accepted = true, ProviderMessageId = $"fake-{m.AttemptId:N}" };
        await RouteAsync(approaching);
        await RouteAsync(approaching);

        Assert.Equal(2, _email.Sent.Count);
        Assert.Equal(_email.Sent[0].AttemptId, _email.Sent[1].AttemptId);
    }

    [Fact]
    public async Task Refused_caregiver_message_is_retried()
    {
        var scheduled = await BookedWithConsentAsync("SYN Breast Imaging Clinic");
        _email.Answer = _ => new SendResult { Accepted = false, FailureReason = "PLACEHOLDER: inbox unavailable" };

        await Assert.ThrowsAsync<NotificationNotSentException>(() => RouteAsync(scheduled));
    }

    [Fact]
    public async Task Patient_refusal_on_one_channel_does_not_mark_unreachable_while_another_channel_is_on_its_way()
    {
        var sms = new FakeProvider(NotificationChannel.Sms, allowsSensitiveContent: false)
        {
            Answer = _ => new SendResult { Accepted = false, FailureReason = "PLACEHOLDER: gateway down" },
        };
        var requested = await _store.ReviewedCaseAsync();
        var before = await _store.TheCaseAsync();

        await RouteAsync(requested, sms);

        Assert.Single(_email.Sent);
        var @case = await _store.TheCaseAsync();
        Assert.Equal(NotificationAttemptStatus.Failed, Assert.Single(@case.Notification!.Attempts, a => a.Channel == NotificationChannel.Sms).Status);
        Assert.Equal(NotificationAttemptStatus.Sent, Assert.Single(@case.Notification.Attempts, a => a.Channel == NotificationChannel.Email).Status);
        Assert.Equal(NotificationStatus.Sent, @case.Notification.Status);
        Assert.False(@case.Exceptions.HasFlag(CaseExceptions.Unreachable));
        Assert.Equal(before.NextAction, @case.NextAction);
    }

    [Fact]
    public async Task Every_reachable_channel_is_on_the_track_before_anything_is_sent()
    {
        var requested = await _store.ReviewedCaseAsync();
        var sms = new FakeProvider(NotificationChannel.Sms, allowsSensitiveContent: false);
        IReadOnlyList<Guid> knownWhenSmsWasSent = [];
        sms.Answer = m =>
        {
            // What a delivery report arriving during this send would be judged against.
            knownWhenSmsWasSent = [.. _store.TheCaseAsync().GetAwaiter().GetResult().Notification!.Attempts.Select(a => a.Id)];
            return new SendResult { Accepted = true };
        };

        await RouteAsync(requested, sms);

        Assert.Equal(2, knownWhenSmsWasSent.Count);
        Assert.Contains(_email.Sent[0].AttemptId, knownWhenSmsWasSent);
    }

    private const string CaregiverInbox = "SYN-CAREGIVER-INBOX-0001";

    private async Task<AppointmentScheduled> BookedWithConsentAsync(string? location, NotificationChannel channel = NotificationChannel.Email)
    {
        await _store.ReviewedCaseAsync();
        await _store.IngestAsync(Envelopes.Consent(_store.Clock.UtcNow, ConsentUpdateKind.Granted, new ContactPoint { Channel = channel, Address = CaregiverInbox }));
        _store.Clock.Advance(TimeSpan.FromMinutes(5));
        await _store.IngestAsync(Envelopes.Appointment(_store.Clock.UtcNow, AppointmentStatus.Scheduled, scheduledFor: _store.Clock.UtcNow.AddDays(3), location: location));
        _store.Clock.Advance(TimeSpan.FromMinutes(5));
        return Assert.Single(await _store.RaisedAsync<AppointmentScheduled>());
    }

    private EscalationRaised Escalation(Case @case, int level, CaseOwner to) =>
        new() { EventId = Guid.NewGuid(), CaseId = @case.Id, OccurredAt = _store.Clock.UtcNow, Level = level, EscalatedTo = to, CurrentOwner = @case.Owner, Reason = "PLACEHOLDER: review-result overdue" };

    /// <summary>One staff notice went to <paramref name="reference"/>, at the address configured for staff, not at an address taken from the owner.</summary>
    private void AssertStaffNotice(string reference)
    {
        var message = Assert.Single(_email.Sent);
        Assert.Equal(RecipientKind.Staff, message.Recipient.Kind);
        Assert.Equal(reference, message.Recipient.Reference);
        Assert.Equal(NotificationTestSupport.StaffAddress, message.ContactPoint.Address);
        Assert.Equal(NotificationRouter.StaffTemplateKey, message.TemplateKey);
    }

    private async Task RouteAsync(IDomainEvent domainEvent, FakeProvider? sms = null, MessageFormat? format = null)
    {
        await using var db = _store.NewContext();
        var router = new NotificationRouter(
            NotificationTestSupport.Resolver(db, NotificationChannel.Email),
            sms is null ? Sender(_store.Clock, Enabled("Email"), _email) : Sender(_store.Clock, Enabled("Email", "Sms"), _email, sms),
            _log,
            _store.Ingestor(db),
            db,
            NullLogger<NotificationRouter>.Instance,
            format);
        await router.HandleAsync(domainEvent, CancellationToken.None);
    }
}

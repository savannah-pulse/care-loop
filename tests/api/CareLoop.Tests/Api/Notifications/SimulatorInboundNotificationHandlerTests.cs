using System.Text.Json;
using CareLoop.Api.Features.Notifications;
using CareLoop.Api.Infrastructure.Notifications;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Recipients;
using CareLoop.Tests.Fakes;

namespace CareLoop.Tests.Api.Notifications;

/// <summary>
/// What the SMS and email simulator reports back (delivery reports and replies) is translated into notification update events.
/// </summary>
public sealed class SimulatorInboundNotificationHandlerTests : IDisposable
{
    private const string Secret = "SYN-TEST-SECRET";

    private static readonly DateTimeOffset At = new(2026, 11, 13, 9, 30, 0, TimeSpan.Zero);

    private readonly NotificationStore _store = new();
    private Guid _attemptId = Guid.NewGuid();

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task Delivered_translates_to_delivered()
    {
        var @case = await PatientNotifiedAsync();

        var envelope = await TranslateOneAsync(Delivery(@case, "delivered"));

        var payload = Payload(envelope);
        Assert.Equal(NotificationUpdateKind.Delivered, payload.Kind);
        Assert.Equal(NotificationChannel.Sms, payload.Channel);
        Assert.Equal(_attemptId, payload.AttemptId);
        Assert.Equal($"sms/message/{_attemptId:D}", payload.CommunicationReference);
        Assert.Equal(CaseEventType.NotificationUpdated, envelope.Type);
        Assert.Equal(@case.SourceSystem, envelope.SourceSystem);
        Assert.Equal(@case.SourceResultId, envelope.SourceResultId);
        Assert.Equal(At, envelope.OccurredAt);
        Assert.Null(envelope.Actor);
    }

    [Fact]
    public async Task Failed_translates_to_failed_with_reason()
    {
        var @case = await PatientNotifiedAsync();

        var withReason = Payload(await TranslateOneAsync(Delivery(@case, "failed", "PLACEHOLDER: handset switched off")));
        var withoutReason = Payload(await TranslateOneAsync(Delivery(@case, "failed")));

        Assert.Equal(NotificationUpdateKind.Failed, withReason.Kind);
        Assert.Equal("PLACEHOLDER: handset switched off", withReason.FailureReason);
        Assert.False(string.IsNullOrWhiteSpace(withoutReason.FailureReason));
    }

    [Theory]
    [InlineData("1")]
    [InlineData(" 1 \n")]
    public async Task Reply_1_translates_to_acknowledged(string text)
    {
        var @case = await PatientNotifiedAsync();

        Assert.Equal(NotificationUpdateKind.Acknowledged, Payload(await TranslateOneAsync(Reply(@case, text))).Kind);
    }

    [Theory]
    [InlineData("2")]
    [InlineData("hello, which clinic is it?")]
    [InlineData("11")]
    [InlineData("yes")]
    [InlineData("")]
    public async Task Reply_2_or_any_other_text_asks_for_a_call_and_is_never_an_acknowledgement(string text)
    {
        var @case = await PatientNotifiedAsync();

        Assert.Equal(NotificationUpdateKind.CallRequested, Payload(await TranslateOneAsync(Reply(@case, text))).Kind);
    }

    [Fact]
    public async Task No_reply_is_ever_teach_back_or_a_confidence_rating()
    {
        var @case = await PatientNotifiedAsync();

        foreach (var text in new[] { "understood", "I understand", "5", "3", "4 out of 5" })
        {
            var payload = Payload(await TranslateOneAsync(Reply(@case, text)));
            Assert.Equal(NotificationUpdateKind.CallRequested, payload.Kind);
            Assert.Null(payload.Confidence);
        }
    }

    [Fact]
    public async Task Delivery_report_is_never_translated_to_an_acknowledgement()
    {
        var @case = await PatientNotifiedAsync();

        var delivered = Payload(await TranslateOneAsync(Delivery(@case, "delivered")));
        var queued = await TranslateAsync(Delivery(@case, "queued"));
        var unknownType = await TranslateAsync(Delivery(@case, "delivered") with { Type = "read-receipt" });

        Assert.Equal(NotificationUpdateKind.Delivered, delivered.Kind);
        Assert.Empty(queued);
        Assert.Empty(unknownType);
    }

    [Theory]
    [InlineData("caregiver")]
    [InlineData("staff")]
    public async Task Callbacks_about_a_caregiver_or_staff_message_are_not_the_patients(string audience)
    {
        var @case = await PatientNotifiedAsync();
        var metadata = new SimulatorMetadata { CaseId = @case.Id.ToString("D"), Audience = audience };

        Assert.Empty(await TranslateAsync(Delivery(@case, "delivered") with { Metadata = metadata }));
        Assert.Empty(await TranslateAsync(Reply(@case, "1") with { Metadata = metadata }));
    }

    [Fact]
    public async Task Unsigned_wrongly_signed_or_unconfigured_messages_are_refused()
    {
        var @case = await PatientNotifiedAsync();
        var body = Body(Reply(@case, "1"));
        await using var db = _store.NewContext();

        var unsigned = new InboundNotificationMessage { ContentType = "application/json", Body = body, Headers = new Dictionary<string, string>() };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Handler(db).TranslateAsync(unsigned, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Handler(db).TranslateAsync(Signed(body, SimulatorInboundNotificationHandler.SignatureFor(body, "SYN-OTHER-SECRET")), CancellationToken.None));
        var tampered = Signed(body) with { Body = body.Replace("SYN-REPLY-1", "SYN-REPLY-9", StringComparison.Ordinal) };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Handler(db).TranslateAsync(tampered, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Handler(db, secret: null).TranslateAsync(Signed(body), CancellationToken.None));
        Assert.Single(await Handler(db).TranslateAsync(Signed(body), CancellationToken.None));
    }

    [Fact]
    public async Task Callback_must_name_a_patient_attempt_of_the_case_it_is_about()
    {
        var @case = await PatientNotifiedAsync();
        var staffAttempt = NotificationSender.AttemptIdFor(Guid.NewGuid(), @case.Owner.Reference, NotificationChannel.Sms).ToString("D");
        var emailAttempt = NotificationSender.AttemptIdFor(
            Assert.Single(await _store.RaisedAsync<CareLoop.Domain.Events.DomainEvents.NotificationRequested>()).EventId,
            @case.PatientReference,
            NotificationChannel.Email).ToString("D");

        await Assert.ThrowsAsync<FormatException>(() => TranslateAsync(Reply(@case, "1") with { InReplyTo = null }));
        await Assert.ThrowsAsync<FormatException>(() => TranslateAsync(Reply(@case, "1") with { InReplyTo = Guid.NewGuid().ToString("D") }));
        await Assert.ThrowsAsync<FormatException>(() => TranslateAsync(Reply(@case, "1") with { InReplyTo = staffAttempt }));
        await Assert.ThrowsAsync<FormatException>(() => TranslateAsync(Reply(@case, "1") with { InReplyTo = emailAttempt }));
        await Assert.ThrowsAsync<FormatException>(() => TranslateAsync(Delivery(@case, "delivered") with { MessageId = staffAttempt }));
        Assert.Single(await TranslateAsync(Reply(@case, "1")));
    }

    [Fact]
    public async Task Reply_must_come_from_an_address_of_the_patient_on_this_channel()
    {
        var @case = await PatientNotifiedAsync();

        await Assert.ThrowsAsync<FormatException>(() => TranslateAsync(Reply(@case, "1") with { From = "SYN-TEL-9999" }));
        await Assert.ThrowsAsync<FormatException>(() => TranslateAsync(Reply(@case, "1") with { From = null }));
        Assert.Single(await TranslateAsync(Reply(@case, "1") with { From = " syn-tel-0001 " }));
    }

    [Fact]
    public async Task Same_callback_gives_the_same_source_event_id()
    {
        var @case = await PatientNotifiedAsync();

        var first = await TranslateOneAsync(Reply(@case, "2"));
        var again = await TranslateOneAsync(Reply(@case, "2"));
        var next = await TranslateOneAsync(Reply(@case, "2") with { ReplyId = "SYN-REPLY-2" });
        var delivered = await TranslateOneAsync(Delivery(@case, "delivered"));
        var failed = await TranslateOneAsync(Delivery(@case, "failed"));

        Assert.Equal("sms/reply/SYN-REPLY-1", first.SourceEventId);
        Assert.Equal(first.SourceEventId, again.SourceEventId);
        Assert.Equal("sms/reply/SYN-REPLY-2", next.SourceEventId);
        Assert.Equal($"sms/message/{_attemptId:D}/delivery/delivered", delivered.SourceEventId);
        Assert.Equal($"sms/message/{_attemptId:D}/delivery/failed", failed.SourceEventId);
    }

    [Fact]
    public async Task Body_that_is_not_a_simulator_callback_is_a_format_error()
    {
        var @case = await PatientNotifiedAsync();
        var unknownCase = new SimulatorMetadata { CaseId = Guid.NewGuid().ToString("D"), Audience = "patient" };

        await Assert.ThrowsAsync<FormatException>(() => TranslateAsync("{ not json"));
        await Assert.ThrowsAsync<FormatException>(() => TranslateAsync("null"));
        await Assert.ThrowsAsync<FormatException>(() => TranslateAsync(Reply(@case, "1") with { Metadata = null }));
        await Assert.ThrowsAsync<FormatException>(() => TranslateAsync(Reply(@case, "1") with { Metadata = unknownCase }));
        await Assert.ThrowsAsync<FormatException>(() => TranslateAsync(Reply(@case, "1") with { Metadata = unknownCase with { CaseId = "not-a-case" } }));
        await Assert.ThrowsAsync<FormatException>(() => TranslateAsync(Reply(@case, "1") with { Text = null }));
        await Assert.ThrowsAsync<FormatException>(() => TranslateAsync(Reply(@case, "1") with { ReplyId = " " }));
        await Assert.ThrowsAsync<FormatException>(() => TranslateAsync(Reply(@case, "1") with { At = null }));
        await Assert.ThrowsAsync<FormatException>(() => TranslateAsync(Delivery(@case, "delivered") with { MessageId = "not-an-id" }));
    }

    [Fact]
    public async Task Email_handler_accepts_only_email_attempts()
    {
        var requested = await _store.ReviewedCaseAsync();
        var @case = await _store.TheCaseAsync();
        var emailAttempt = NotificationSender.AttemptIdFor(requested.EventId, requested.PatientReference, NotificationChannel.Email);
        var smsAttempt = NotificationSender.AttemptIdFor(requested.EventId, requested.PatientReference, NotificationChannel.Sms);
        await using var db = _store.NewContext();
        var handler = new EmailInboundNotificationHandler(db, NotificationTestSupport.Resolver(db), NotificationTestSupport.Simulator(inboundSecret: Secret));

        _attemptId = emailAttempt;
        var payload = Payload(Assert.Single(await handler.TranslateAsync(Signed(Body(Delivery(@case, "delivered"))), CancellationToken.None)));
        _attemptId = smsAttempt;

        Assert.Equal(NotificationChannel.Email, payload.Channel);
        Assert.Equal($"email/message/{emailAttempt:D}", payload.CommunicationReference);
        await Assert.ThrowsAsync<FormatException>(() => handler.TranslateAsync(Signed(Body(Delivery(@case, "delivered"))), CancellationToken.None));
    }

    [Fact]
    public async Task Translated_callbacks_apply_to_the_case()
    {
        var @case = await PatientNotifiedAsync();

        foreach (var callback in new[] { Delivery(@case, "delivered"), Reply(@case, "1") })
        {
            Assert.Equal(CareLoop.Api.Features.Events.IngestOutcome.Applied, (await _store.IngestAsync(await TranslateOneAsync(callback))).Outcome);
        }

        var after = await _store.TheCaseAsync();
        Assert.Equal(_attemptId, Assert.Single(after.Notification!.Attempts).Id);
        Assert.Equal(CareLoop.Domain.Tracks.NotificationStatus.Acknowledged, after.Notification.Status);
    }

    [Fact]
    public async Task Unreadable_reply_gives_the_owner_a_call_back_and_leaves_the_message_unacknowledged()
    {
        var @case = await PatientNotifiedAsync();
        await _store.IngestAsync(await TranslateOneAsync(Delivery(@case, "delivered")));

        var result = await _store.IngestAsync(await TranslateOneAsync(Reply(@case, "what is this about?")));

        Assert.Equal(CareLoop.Api.Features.Events.IngestOutcome.Applied, result.Outcome);
        var after = await _store.TheCaseAsync();
        Assert.Equal(CareLoop.Domain.Tracks.NotificationStatus.Delivered, after.Notification!.Status);
        Assert.Single(await _store.RaisedAsync<CareLoop.Domain.Events.DomainEvents.CallRequested>());
    }

    /// <summary>A reviewed case whose patient notification went out by text message; <see cref="_attemptId"/> is that attempt.</summary>
    private async Task<Case> PatientNotifiedAsync()
    {
        var requested = await _store.ReviewedCaseAsync();
        _attemptId = NotificationSender.AttemptIdFor(requested.EventId, requested.PatientReference, NotificationChannel.Sms);
        return await _store.TheCaseAsync();
    }

    private SimulatorCallback Delivery(Case @case, string status, string? reason = null) => new()
    {
        Type = "delivery",
        MessageId = _attemptId.ToString("D"),
        Status = status,
        Reason = reason,
        At = At,
        Metadata = new SimulatorMetadata { CaseId = @case.Id.ToString("D"), Audience = "patient" },
    };

    private SimulatorCallback Reply(Case @case, string text) => new()
    {
        Type = "reply",
        ReplyId = "SYN-REPLY-1",
        InReplyTo = _attemptId.ToString("D"),
        From = Envelopes.PatientPhone,
        Text = text,
        At = At,
        Metadata = new SimulatorMetadata { CaseId = @case.Id.ToString("D"), Audience = "patient" },
    };

    private static string Body(SimulatorCallback callback) => JsonSerializer.Serialize(callback, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private async Task<EventEnvelope> TranslateOneAsync(SimulatorCallback callback) =>
        Assert.Single(await TranslateAsync(callback));

    private Task<IReadOnlyList<EventEnvelope>> TranslateAsync(SimulatorCallback callback) => TranslateAsync(Body(callback));

    private async Task<IReadOnlyList<EventEnvelope>> TranslateAsync(string body)
    {
        await using var db = _store.NewContext();
        return await Handler(db).TranslateAsync(Signed(body), CancellationToken.None);
    }

    private static SmsInboundNotificationHandler Handler(CareLoopDbContext db, string? secret = Secret) =>
        new(db, NotificationTestSupport.Resolver(db), NotificationTestSupport.Simulator(inboundSecret: secret));

    private static InboundNotificationMessage Signed(string body, string? signature = null) => new()
    {
        ContentType = "application/json",
        Body = body,
        Headers = new Dictionary<string, string> { [SimulatorInboundNotificationHandler.SignatureHeader] = signature ?? SimulatorInboundNotificationHandler.SignatureFor(body, Secret) },
    };

    private static NotificationUpdatePayload Payload(EventEnvelope envelope) => Assert.IsType<NotificationUpdatePayload>(envelope.Payload);
}

using System.Text;
using CareLoop.Api.Features.Notifications;
using CareLoop.Api.Infrastructure.Notifications;
using CareLoop.Contracts.Events;
using CareLoop.Domain.Recipients;
using CareLoop.Domain.Tracks;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace CareLoop.Tests.Api.Notifications;

/// <summary>
/// The provider callback endpoint translates with the provider's own handler and ingests the result.
/// </summary>
public sealed class ReceiveInboundNotificationTests : IDisposable
{
    private const string Secret = "SYN-TEST-SECRET";

    private readonly NotificationStore _store = new();
    private Guid _attemptId = Guid.NewGuid();

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task Message_for_an_enabled_provider_is_translated_and_ingested()
    {
        var caseId = await PatientNotifiedAsync();

        var status = await PostAsync("sms", Delivery(caseId));

        Assert.Equal(StatusCodes.Status200OK, status);
        var @case = await _store.TheCaseAsync();
        Assert.Equal(NotificationStatus.Delivered, @case.Notification!.Status);
        Assert.Equal(_attemptId, Assert.Single(@case.Notification.Attempts).Id);
    }

    [Fact]
    public async Task Redelivered_message_is_deduplicated()
    {
        var caseId = await PatientNotifiedAsync();
        var body = Reply(caseId, "1");

        Assert.Equal(StatusCodes.Status200OK, await PostAsync("Sms", body));
        var afterFirst = await _store.TheCaseAsync();
        Assert.Equal(StatusCodes.Status200OK, await PostAsync("Sms", body));

        var afterSecond = await _store.TheCaseAsync();
        Assert.Equal(NotificationStatus.Acknowledged, afterSecond.Notification!.Status);
        Assert.Equal(afterFirst.Version, afterSecond.Version);
    }

    [Fact]
    public async Task Unknown_provider_returns_404()
    {
        var caseId = await PatientNotifiedAsync();
        var body = Reply(caseId, "1");

        Assert.Equal(StatusCodes.Status404NotFound, await PostAsync("pigeon", body));
        Assert.Equal(StatusCodes.Status404NotFound, await PostAsync("simulator", body));
        Assert.Equal(StatusCodes.Status404NotFound, await PostAsync("1", body));
    }

    [Fact]
    public async Task Provider_that_is_not_enabled_returns_404()
    {
        var caseId = await PatientNotifiedAsync();
        var body = Reply(caseId, "1");

        Assert.Equal(StatusCodes.Status404NotFound, await PostAsync("sms", body, enabled: ["Email"]));
        Assert.Equal(StatusCodes.Status404NotFound, await PostAsync("email", body, enabled: ["Sms"]));
    }

    [Fact]
    public async Task Message_that_cannot_be_verified_returns_401()
    {
        await using var db = _store.NewContext();
        var result = await ReceiveInboundNotification.Handle(
            "sms",
            Request("{}"),
            [new StubHandler(() => throw new UnauthorizedAccessException("The signature does not match."))],
            Options.Create(new NotificationOptions { EnabledProviders = ["Sms"] }),
            _store.Ingestor(db),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status401Unauthorized, StatusOf(result));
    }

    [Fact]
    public async Task Unsigned_simulator_message_returns_401_and_changes_nothing()
    {
        var caseId = await PatientNotifiedAsync();
        var before = await _store.TheCaseAsync();

        Assert.Equal(StatusCodes.Status401Unauthorized, await PostAsync("sms", Reply(caseId, "1"), signed: false));

        var after = await _store.TheCaseAsync();
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(before.Notification!.Status, after.Notification!.Status);
    }

    [Fact]
    public async Task Message_in_the_wrong_format_returns_400()
    {
        var caseId = await PatientNotifiedAsync();

        Assert.Equal(StatusCodes.Status400BadRequest, await PostAsync("sms", "{ not json"));
        Assert.Equal(StatusCodes.Status400BadRequest, await PostAsync("sms", """{ "resourceType": "Communication", "type": "reply" }"""));
        Assert.Equal(StatusCodes.Status400BadRequest, await PostAsync("sms", Reply(caseId, "1", from: "SYN-TEL-9999")));
    }

    [Fact]
    public async Task Event_refused_by_a_rule_returns_422()
    {
        // A confidence rating outside 1 to 5 is refused by the notification rule.
        await PatientNotifiedAsync();
        var refused = Envelopes.Notification(_store.Clock.UtcNow, NotificationUpdateKind.ConfidenceRated, confidence: 9);
        await using var db = _store.NewContext();

        var result = await ReceiveInboundNotification.Handle(
            "sms",
            Request("{}"),
            [new StubHandler(() => [refused])],
            Options.Create(new NotificationOptions { EnabledProviders = ["Sms"] }),
            _store.Ingestor(db),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, StatusOf(result));
    }

    /// <summary>Reviews a case so its patient notification goes out, and points <see cref="_attemptId"/> at that attempt.</summary>
    private async Task<Guid> PatientNotifiedAsync()
    {
        var requested = await _store.ReviewedCaseAsync();
        _attemptId = NotificationSender.AttemptIdFor(requested.EventId, requested.PatientReference, NotificationChannel.Sms);
        return requested.CaseId;
    }

    private string Delivery(Guid caseId) => Body(new SimulatorCallback
    {
        Type = "delivery",
        MessageId = _attemptId.ToString("D"),
        Status = "delivered",
        At = _store.Clock.UtcNow,
        Metadata = new SimulatorMetadata { CaseId = caseId.ToString("D"), Audience = "patient" },
    });

    private string Reply(Guid caseId, string text, string from = Envelopes.PatientPhone) => Body(new SimulatorCallback
    {
        Type = "reply",
        ReplyId = $"{_attemptId:D}-r1",
        InReplyTo = _attemptId.ToString("D"),
        From = from,
        Text = text,
        At = _store.Clock.UtcNow,
        Metadata = new SimulatorMetadata { CaseId = caseId.ToString("D"), Audience = "patient" },
    });

    private static string Body(SimulatorCallback callback) =>
        System.Text.Json.JsonSerializer.Serialize(callback, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

    private async Task<int> PostAsync(string provider, string body, string[]? enabled = null, bool signed = true)
    {
        await using var db = _store.NewContext();
        var simulator = NotificationTestSupport.Simulator(inboundSecret: Secret);
        var recipients = NotificationTestSupport.Resolver(db);
        var result = await ReceiveInboundNotification.Handle(
            provider,
            Request(body, signed ? SimulatorInboundNotificationHandler.SignatureFor(body, Secret) : null),
            [new SmsInboundNotificationHandler(db, recipients, simulator), new EmailInboundNotificationHandler(db, recipients, simulator)],
            Options.Create(new NotificationOptions { EnabledProviders = [.. enabled ?? ["Sms", "Email"]] }),
            _store.Ingestor(db),
            CancellationToken.None);
        return StatusOf(result);
    }

    private static int StatusOf(IResult result) =>
        Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode ?? StatusCodes.Status200OK;

    private static HttpRequest Request(string body, string? signature = null)
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/json";
        if (signature is not null)
        {
            context.Request.Headers[SimulatorInboundNotificationHandler.SignatureHeader] = signature;
        }

        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return context.Request;
    }

    private sealed class StubHandler(Func<IReadOnlyList<EventEnvelope>> translate) : IInboundNotificationHandler
    {
        public NotificationChannel Channel => NotificationChannel.Sms;

        public Task<IReadOnlyList<EventEnvelope>> TranslateAsync(InboundNotificationMessage message, CancellationToken ct) =>
            Task.FromResult(translate());
    }
}

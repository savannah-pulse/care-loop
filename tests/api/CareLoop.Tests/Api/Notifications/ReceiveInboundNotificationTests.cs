using System.Text;
using CareLoop.Api.Features.Fhir;
using CareLoop.Api.Features.Notifications;
using CareLoop.Api.Infrastructure.Notifications;
using CareLoop.Contracts.Events;
using CareLoop.Domain.Recipients;
using CareLoop.Domain.Tracks;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace CareLoop.Tests.Api.Notifications;

/// <summary>
/// The provider callback endpoint translates with the provider's own handler and ingests the result.
/// </summary>
public sealed class ReceiveInboundNotificationTests : IDisposable
{
    private readonly NotificationStore _store = new();
    private readonly Guid _attemptId = Guid.NewGuid();

    public void Dispose() => _store.Dispose();

    [Fact]
    public async System.Threading.Tasks.Task Message_for_an_enabled_provider_is_translated_and_ingested()
    {
        var caseId = (await _store.ReviewedCaseAsync()).CaseId;

        var status = await PostAsync("simulator", Report(caseId, StatusMap.DeliveryStatusCategory));

        Assert.Equal(StatusCodes.Status200OK, status);
        var @case = await _store.TheCaseAsync();
        Assert.Equal(NotificationStatus.Delivered, @case.Notification!.Status);
        Assert.Equal(_attemptId, Assert.Single(@case.Notification.Attempts).Id);
    }

    [Fact]
    public async System.Threading.Tasks.Task Redelivered_message_is_deduplicated()
    {
        var caseId = (await _store.ReviewedCaseAsync()).CaseId;
        var body = Report(caseId, StatusMap.AcknowledgementCategory);

        Assert.Equal(StatusCodes.Status200OK, await PostAsync("Simulator", body));
        var afterFirst = await _store.TheCaseAsync();
        Assert.Equal(StatusCodes.Status200OK, await PostAsync("Simulator", body));

        var afterSecond = await _store.TheCaseAsync();
        Assert.Equal(NotificationStatus.Acknowledged, afterSecond.Notification!.Status);
        Assert.Equal(afterFirst.Version, afterSecond.Version);
    }

    [Fact]
    public async System.Threading.Tasks.Task Unknown_provider_returns_404()
    {
        var caseId = (await _store.ReviewedCaseAsync()).CaseId;
        var body = Report(caseId, StatusMap.AcknowledgementCategory);

        Assert.Equal(StatusCodes.Status404NotFound, await PostAsync("pigeon", body));
        Assert.Equal(StatusCodes.Status404NotFound, await PostAsync("0", body));
    }

    [Fact]
    public async System.Threading.Tasks.Task Provider_that_is_not_enabled_returns_404()
    {
        var caseId = (await _store.ReviewedCaseAsync()).CaseId;
        var body = Report(caseId, StatusMap.AcknowledgementCategory);

        Assert.Equal(StatusCodes.Status404NotFound, await PostAsync("simulator", body, enabled: ["Sms"]));
        Assert.Equal(StatusCodes.Status404NotFound, await PostAsync("sms", body, enabled: ["Simulator", "Sms"]));
    }

    [Fact]
    public async System.Threading.Tasks.Task Message_that_cannot_be_verified_returns_401()
    {
        await using var db = _store.NewContext();
        var result = await ReceiveInboundNotification.Handle(
            "simulator",
            Request("{}"),
            [new UnverifiableHandler()],
            Options.Create(new NotificationOptions { EnabledProviders = ["Simulator"] }),
            _store.Ingestor(db),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status401Unauthorized, StatusOf(result));
    }

    [Fact]
    public async System.Threading.Tasks.Task Message_in_the_wrong_format_returns_400()
    {
        await _store.ReviewedCaseAsync();

        Assert.Equal(StatusCodes.Status400BadRequest, await PostAsync("simulator", "{ not json"));
        Assert.Equal(StatusCodes.Status400BadRequest, await PostAsync("simulator", FhirJsonCodec.Serialize(new Patient { Id = "SYN-PT-0001" })));
    }

    [Fact]
    public async System.Threading.Tasks.Task Event_refused_by_a_rule_returns_422()
    {
        // No notification has been requested on an unreviewed case, so there is nothing to report on.
        var @case = await _store.OpenCaseAsync();

        var status = await PostAsync("simulator", Report(@case.Id, StatusMap.DeliveryStatusCategory));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, status);
    }

    private string Report(Guid caseId, string category) => FhirJsonCodec.Serialize(new Communication
    {
        Id = $"SYN-COMM-{category}",
        Meta = new Meta { VersionId = "1" },
        Status = EventStatus.Completed,
        Category = [new CodeableConcept(StatusMap.CommunicationCategorySystem, category)],
        BasedOn = [new ResourceReference($"CommunicationRequest/{_attemptId:D}")],
        About = [new ResourceReference($"Task/{caseId:D}")],
        SentElement = new FhirDateTime(_store.Clock.UtcNow),
    });

    private async System.Threading.Tasks.Task<int> PostAsync(string provider, string body, string[]? enabled = null)
    {
        await using var db = _store.NewContext();
        var result = await ReceiveInboundNotification.Handle(
            provider,
            Request(body),
            [new SimulatorInboundNotificationHandler(db)],
            Options.Create(new NotificationOptions { EnabledProviders = [.. enabled ?? ["Simulator"]] }),
            _store.Ingestor(db),
            CancellationToken.None);
        return StatusOf(result);
    }

    private static int StatusOf(IResult result) =>
        Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode ?? StatusCodes.Status200OK;

    private static HttpRequest Request(string body)
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = FhirEndpoints.FhirJson;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return context.Request;
    }

    private sealed class UnverifiableHandler : IInboundNotificationHandler
    {
        public NotificationChannel Channel => NotificationChannel.Simulator;

        public System.Threading.Tasks.Task<IReadOnlyList<EventEnvelope>> TranslateAsync(InboundNotificationMessage message, CancellationToken ct) =>
            throw new UnauthorizedAccessException("The signature does not match.");
    }
}

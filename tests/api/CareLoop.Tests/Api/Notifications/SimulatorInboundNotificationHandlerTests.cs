using CareLoop.Api.Features.Fhir;
using CareLoop.Api.Features.Notifications;
using CareLoop.Api.Infrastructure.Notifications;
using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Recipients;
using CareLoop.Tests.Fakes;
using CareLoop.Api.Infrastructure.Persistence;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Options;

namespace CareLoop.Tests.Api.Notifications;

/// <summary>
/// What the patient-channel simulator reports back is translated into notification update events.
/// </summary>
public sealed class SimulatorInboundNotificationHandlerTests : IDisposable
{
    private const string Secret = "SYN-TEST-SECRET";

    private readonly NotificationStore _store = new();
    private readonly Guid _attemptId = Guid.NewGuid();

    public void Dispose() => _store.Dispose();

    [Fact]
    public async System.Threading.Tasks.Task Completed_translates_to_delivered()
    {
        var @case = await _store.OpenCaseAsync();

        var envelope = await TranslateOneAsync(Report(@case, StatusMap.DeliveryStatusCategory, EventStatus.Completed));

        var payload = Payload(envelope);
        Assert.Equal(NotificationUpdateKind.Delivered, payload.Kind);
        Assert.Equal(NotificationChannel.Simulator, payload.Channel);
        Assert.Equal(_attemptId, payload.AttemptId);
        Assert.Equal("Communication/SYN-COMM-1", payload.CommunicationReference);
        Assert.Equal(CaseEventType.NotificationUpdated, envelope.Type);
        Assert.Equal(@case.SourceSystem, envelope.SourceSystem);
        Assert.Equal(@case.SourceResultId, envelope.SourceResultId);
        Assert.Equal(new DateTimeOffset(2026, 11, 13, 9, 30, 0, TimeSpan.Zero), envelope.OccurredAt);
        Assert.Null(envelope.Actor);
    }

    [Fact]
    public async System.Threading.Tasks.Task Not_done_translates_to_failed_with_reason()
    {
        var @case = await _store.OpenCaseAsync();
        var report = Report(@case, StatusMap.DeliveryStatusCategory, EventStatus.NotDone);
        report.StatusReason = new CodeableConcept { Text = "PLACEHOLDER: handset switched off" };

        var payload = Payload(await TranslateOneAsync(report));

        Assert.Equal(NotificationUpdateKind.Failed, payload.Kind);
        Assert.Equal("PLACEHOLDER: handset switched off", payload.FailureReason);
    }

    [Fact]
    public async System.Threading.Tasks.Task Patient_acknowledgement_category_translates_to_acknowledged()
    {
        var @case = await _store.OpenCaseAsync();

        Assert.Equal(NotificationUpdateKind.Acknowledged, Payload(await TranslateOneAsync(Report(@case, StatusMap.AcknowledgementCategory))).Kind);
    }

    [Fact]
    public async System.Threading.Tasks.Task Teach_back_category_translates_to_understood()
    {
        var @case = await _store.OpenCaseAsync();

        Assert.Equal(NotificationUpdateKind.Understood, Payload(await TranslateOneAsync(Report(@case, StatusMap.TeachBackCategory))).Kind);
    }

    [Fact]
    public async System.Threading.Tasks.Task Call_me_category_translates_to_call_requested()
    {
        var @case = await _store.OpenCaseAsync();

        Assert.Equal(NotificationUpdateKind.CallRequested, Payload(await TranslateOneAsync(Report(@case, StatusMap.CallMeCategory))).Kind);
    }

    [Fact]
    public async System.Threading.Tasks.Task Confidence_rating_category_translates_to_a_rating_with_its_value()
    {
        var @case = await _store.OpenCaseAsync();
        var report = Report(@case, StatusMap.ConfidenceRatingCategory);
        report.Payload = [new Communication.PayloadComponent { Content = new FhirString("4") }];

        var payload = Payload(await TranslateOneAsync(report));

        Assert.Equal(NotificationUpdateKind.ConfidenceRated, payload.Kind);
        Assert.Equal(4, payload.Confidence);
    }

    [Fact]
    public async System.Threading.Tasks.Task Delivery_report_is_never_translated_to_an_acknowledgement()
    {
        var @case = await _store.OpenCaseAsync();

        var completed = Payload(await TranslateOneAsync(Report(@case, StatusMap.DeliveryStatusCategory, EventStatus.Completed)));
        var inProgress = await TranslateAsync(Report(@case, StatusMap.DeliveryStatusCategory, EventStatus.InProgress));
        var unfinishedAcknowledgement = await TranslateAsync(Report(@case, StatusMap.AcknowledgementCategory, EventStatus.InProgress));

        Assert.Equal(NotificationUpdateKind.Delivered, completed.Kind);
        Assert.Empty(inProgress);
        Assert.Empty(unfinishedAcknowledgement);
    }

    [Fact]
    public async System.Threading.Tasks.Task Reports_about_a_caregiver_message_are_not_the_patients()
    {
        var @case = await _store.OpenCaseAsync();
        var delivered = Report(@case, StatusMap.DeliveryStatusCategory, EventStatus.Completed);
        delivered.BasedOn = [new ResourceReference($"CommunicationRequest/{SimulatorNotificationProvider.CaregiverIdPrefix}{_attemptId:D}")];
        var acknowledged = Report(@case, StatusMap.AcknowledgementCategory, id: "SYN-COMM-2");
        acknowledged.BasedOn = delivered.BasedOn;

        Assert.Empty(await TranslateAsync(delivered));
        Assert.Empty(await TranslateAsync(acknowledged));
    }

    [Fact]
    public async System.Threading.Tasks.Task Unsigned_wrongly_signed_or_unconfigured_messages_are_refused()
    {
        var @case = await _store.OpenCaseAsync();
        var body = FhirJsonCodec.Serialize(Report(@case, StatusMap.AcknowledgementCategory));
        await using var db = _store.NewContext();

        var unsigned = new InboundNotificationMessage { ContentType = FhirEndpoints.FhirJson, Body = body, Headers = new Dictionary<string, string>() };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Handler(db).TranslateAsync(unsigned, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Handler(db).TranslateAsync(Signed(body, SimulatorInboundNotificationHandler.SignatureFor(body, "SYN-OTHER-SECRET")), CancellationToken.None));
        var tampered = Signed(body) with { Body = body.Replace("SYN-COMM-1", "SYN-COMM-9", StringComparison.Ordinal) };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Handler(db).TranslateAsync(tampered, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Handler(db, secret: null).TranslateAsync(Signed(body), CancellationToken.None));
        Assert.Single(await Handler(db).TranslateAsync(Signed(body), CancellationToken.None));
    }

    [Fact]
    public async System.Threading.Tasks.Task Same_communication_version_gives_the_same_source_event_id()
    {
        var @case = await _store.OpenCaseAsync();
        var report = Report(@case, StatusMap.AcknowledgementCategory);

        var first = await TranslateOneAsync(report);
        var again = await TranslateOneAsync(report);
        report.Meta!.VersionId = "2";
        var next = await TranslateOneAsync(report);

        Assert.Equal("Communication/SYN-COMM-1/_history/1", first.SourceEventId);
        Assert.Equal(first.SourceEventId, again.SourceEventId);
        Assert.Equal("Communication/SYN-COMM-1/_history/2", next.SourceEventId);
    }

    [Fact]
    public async System.Threading.Tasks.Task Body_that_is_not_a_Communication_is_a_format_error()
    {
        var @case = await _store.OpenCaseAsync();
        var noCase = Report(@case, StatusMap.AcknowledgementCategory);
        noCase.About = [];
        var unknownCase = Report(@case, StatusMap.AcknowledgementCategory);
        unknownCase.About = [new ResourceReference($"Task/{Guid.NewGuid():D}")];

        await Assert.ThrowsAsync<FormatException>(() => TranslateAsync("{ not json"));
        await Assert.ThrowsAsync<FormatException>(() => TranslateAsync(FhirJsonCodec.Serialize(new Patient { Id = "SYN-PT-0001" })));
        await Assert.ThrowsAsync<FormatException>(() => TranslateAsync(noCase));
        await Assert.ThrowsAsync<FormatException>(() => TranslateAsync(unknownCase));
    }

    [Fact]
    public async System.Threading.Tasks.Task Translated_replies_apply_to_the_case()
    {
        await _store.ReviewedCaseAsync();
        var @case = await _store.TheCaseAsync();

        foreach (var report in new[]
        {
            Report(@case, StatusMap.DeliveryStatusCategory, EventStatus.Completed, "SYN-COMM-1"),
            Report(@case, StatusMap.AcknowledgementCategory, id: "SYN-COMM-2"),
        })
        {
            Assert.Equal(CareLoop.Api.Features.Events.IngestOutcome.Applied, (await _store.IngestAsync(await TranslateOneAsync(report))).Outcome);
        }

        var after = await _store.TheCaseAsync();
        Assert.Equal(_attemptId, Assert.Single(after.Notification!.Attempts).Id);
        Assert.Equal(CareLoop.Domain.Tracks.NotificationStatus.Acknowledged, after.Notification.Status);
    }

    private Communication Report(Case @case, string category, EventStatus status = EventStatus.Completed, string id = "SYN-COMM-1") => new()
    {
        Id = id,
        Meta = new Meta { VersionId = "1", LastUpdated = new DateTimeOffset(2026, 11, 13, 9, 45, 0, TimeSpan.Zero) },
        Status = status,
        Category = [new CodeableConcept(StatusMap.CommunicationCategorySystem, category)],
        BasedOn = [new ResourceReference($"CommunicationRequest/{_attemptId:D}")],
        About = [new ResourceReference($"Task/{@case.Id:D}")],
        SentElement = new FhirDateTime(new DateTimeOffset(2026, 11, 13, 9, 30, 0, TimeSpan.Zero)),
    };

    private async System.Threading.Tasks.Task<EventEnvelope> TranslateOneAsync(Communication communication) =>
        Assert.Single(await TranslateAsync(communication));

    private System.Threading.Tasks.Task<IReadOnlyList<EventEnvelope>> TranslateAsync(Communication communication) =>
        TranslateAsync(FhirJsonCodec.Serialize(communication));

    private async System.Threading.Tasks.Task<IReadOnlyList<EventEnvelope>> TranslateAsync(string body)
    {
        await using var db = _store.NewContext();
        return await Handler(db).TranslateAsync(Signed(body), CancellationToken.None);
    }

    private static SimulatorInboundNotificationHandler Handler(CareLoopDbContext db, string? secret = Secret) =>
        new(db, Options.Create(new SimulatorNotificationOptions { InboundSecret = secret }));

    private static InboundNotificationMessage Signed(string body, string? signature = null) => new()
    {
        ContentType = FhirEndpoints.FhirJson,
        Body = body,
        Headers = new Dictionary<string, string> { [SimulatorInboundNotificationHandler.SignatureHeader] = signature ?? SimulatorInboundNotificationHandler.SignatureFor(body, Secret) },
    };

    private static NotificationUpdatePayload Payload(EventEnvelope envelope) => Assert.IsType<NotificationUpdatePayload>(envelope.Payload);
}

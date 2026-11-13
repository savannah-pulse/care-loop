using System.Text.Json;
using System.Text.Json.Serialization;
using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Time;

namespace CareLoop.Api.Features.Events;

/// <summary>
/// The one intake pipeline for every inbound event.
/// </summary>
/// <remarks>
/// Every event, whatever its source, goes through the same five steps: validate, deduplicate,
/// append, apply to the projection, audit. Having a single pipeline is what guarantees that no
/// event can change a case without being stored and audited, and that a resend from a hospital
/// system never produces a second case or a second event.
/// </remarks>
public sealed class EventIngestor
{
    /// <summary>
    /// The payload type each source event must carry. The two internal event types are absent on
    /// purpose: they are saved through the repository directly, never ingested.
    /// </summary>
    private static readonly IReadOnlyDictionary<CaseEventType, Type> PayloadTypes = new Dictionary<CaseEventType, Type>
    {
        [CaseEventType.ScreeningResultReceived] = typeof(ScreeningResultPayload),
        [CaseEventType.ClinicalReviewCompleted] = typeof(ClinicalReviewPayload),
        [CaseEventType.NotificationUpdated] = typeof(NotificationUpdatePayload),
        [CaseEventType.ConsultationUpdated] = typeof(ConsultationUpdatePayload),
        [CaseEventType.ReferralUpdated] = typeof(ReferralUpdatePayload),
        [CaseEventType.AppointmentUpdated] = typeof(AppointmentUpdatePayload),
        [CaseEventType.OutcomeRecorded] = typeof(OutcomePayload),
        [CaseEventType.ClosureDecided] = typeof(ClosurePayload),
        [CaseEventType.ConsentRecorded] = typeof(ConsentPayload),
    };

    private static readonly JsonSerializerOptions PayloadSerializerOptions = CreatePayloadSerializerOptions();

    private readonly ICaseRepository _cases;
    private readonly IEnumerable<IEventHandler> _handlers;
    private readonly IClock _clock;
    private readonly ILogger<EventIngestor> _logger;

    /// <summary>
    /// Creates the ingestor.
    /// </summary>
    /// <param name="cases">The single write path for cases.</param>
    /// <param name="handlers">Every registered event handler; one is chosen per event by its event type.</param>
    /// <param name="clock">Supplies the received timestamp.</param>
    /// <param name="logger">Logs intake outcomes without patient detail.</param>
    public EventIngestor(
        ICaseRepository cases,
        IEnumerable<IEventHandler> handlers,
        IClock clock,
        ILogger<EventIngestor> logger)
    {
        _cases = cases;
        _handlers = handlers;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Ingests one event.
    /// </summary>
    /// <param name="envelope">The event to ingest.</param>
    /// <param name="ct">Cancels the operation.</param>
    /// <returns>Whether the event was applied, was a duplicate, was rejected by a rule or was invalid.</returns>
    /// <remarks>
    /// Steps, in order:
    /// <list type="number">
    /// <item><description>Validate the envelope (required fields, payload type matches event type). Invalid: return <see cref="IngestOutcome.Invalid"/> and store nothing.</description></item>
    /// <item><description>Deduplicate with <see cref="ICaseRepository.EventExistsAsync"/>. Already stored: return <see cref="IngestOutcome.Duplicate"/>.</description></item>
    /// <item><description>Find the case with <see cref="ICaseRepository.FindBySourceAsync"/>; only a screening result may arrive without one.</description></item>
    /// <item><description>Run the matching <see cref="IEventHandler"/>. A Domain guard that throws because the event does not fit the case's state counts as a refusal.</description></item>
    /// <item><description>Build the <see cref="SourceEvent"/> (applied or rejected, with reason) and call <see cref="ICaseRepository.SaveAsync"/>, which also writes the audit record and outbox messages.</description></item>
    /// </list>
    /// A refused event (for example closure without evidence) is still saved, so the attempt is
    /// audited. A refused event with no case to attach it to (a screening result that cannot open
    /// one) is invalid instead, because there is nothing to audit it against.
    /// </remarks>
    /// <exception cref="InvalidOperationException">No handler is registered for the event type.</exception>
    public async Task<IngestResult> IngestAsync(EventEnvelope envelope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (Validate(envelope) is { } invalid)
        {
            _logger.LogWarning("Rejected invalid {EventType} from {SourceSystem}: {Reason}", envelope.Type, envelope.SourceSystem, invalid);
            return new IngestResult { Outcome = IngestOutcome.Invalid, Reason = invalid };
        }

        var existing = await _cases.FindBySourceAsync(envelope.SourceSystem, envelope.SourceResultId, ct);

        if (await _cases.EventExistsAsync(envelope.SourceSystem, envelope.SourceEventId, ct))
        {
            _logger.LogInformation("Duplicate {EventType} {SourceEventId} from {SourceSystem} ignored", envelope.Type, envelope.SourceEventId, envelope.SourceSystem);
            return new IngestResult { Outcome = IngestOutcome.Duplicate, CaseId = existing?.Id };
        }

        if (existing is null && envelope.Type != CaseEventType.ScreeningResultReceived)
        {
            var reason = $"No case exists for result '{envelope.SourceSystem}/{envelope.SourceResultId}'; only a screening result may open one.";
            _logger.LogWarning("Rejected {EventType} from {SourceSystem}: {Reason}", envelope.Type, envelope.SourceSystem, reason);
            return new IngestResult { Outcome = IngestOutcome.Invalid, Reason = reason };
        }

        var handler = _handlers.FirstOrDefault(h => h.EventType == envelope.Type)
            ?? throw new InvalidOperationException($"No event handler is registered for {envelope.Type}.");

        EventHandlingResult result;
        try
        {
            result = await handler.HandleAsync(existing, envelope, ct);
        }
        catch (InvalidOperationException ex) when (existing is not null)
        {
            result = new EventHandlingResult { Case = existing, Applied = false, RejectionReason = ex.Message };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Rejected {EventType} from {SourceSystem}: {Reason}", envelope.Type, envelope.SourceSystem, ex.Message);
            return new IngestResult { Outcome = IngestOutcome.Invalid, Reason = ex.Message };
        }

        var sourceEvent = new SourceEvent
        {
            Id = Guid.NewGuid(),
            SourceSystem = envelope.SourceSystem,
            SourceEventId = envelope.SourceEventId,
            Type = envelope.Type,
            CaseId = result.Case.Id,
            Actor = envelope.Actor,
            OccurredAt = envelope.OccurredAt,
            ReceivedAt = _clock.UtcNow,
            PayloadJson = JsonSerializer.Serialize(envelope.Payload, envelope.Payload.GetType(), PayloadSerializerOptions),
            Disposition = result.Applied ? SourceEventDisposition.Applied : SourceEventDisposition.Rejected,
            DispositionReason = result.RejectionReason,
            EvidenceReference = result.EvidenceReference,
        };

        await _cases.SaveAsync(result.Case, sourceEvent, ct);

        if (result.Applied)
        {
            _logger.LogInformation("Applied {EventType} to case {CaseId}", envelope.Type, result.Case.Id);
            return new IngestResult { Outcome = IngestOutcome.Applied, CaseId = result.Case.Id };
        }

        _logger.LogInformation("Rejected {EventType} on case {CaseId}: {Reason}", envelope.Type, result.Case.Id, result.RejectionReason);
        return new IngestResult { Outcome = IngestOutcome.Rejected, CaseId = result.Case.Id, Reason = result.RejectionReason };
    }

    /// <summary>
    /// Checks the envelope's shape. Returns the problem, or <see langword="null"/> when it is valid.
    /// </summary>
    private static string? Validate(EventEnvelope envelope)
    {
        if (string.IsNullOrWhiteSpace(envelope.SourceSystem))
        {
            return "The event names no source system.";
        }

        if (string.IsNullOrWhiteSpace(envelope.SourceEventId))
        {
            return "The event has no source event identifier, so it cannot be deduplicated.";
        }

        if (string.IsNullOrWhiteSpace(envelope.SourceResultId))
        {
            return "The event names no source result, so it cannot be matched to a case.";
        }

        if (envelope.Payload is null)
        {
            return "The event carries no payload.";
        }

        if (!PayloadTypes.TryGetValue(envelope.Type, out var expected))
        {
            return $"{envelope.Type} is not an inbound event type; it is raised inside CareLoop and saved directly.";
        }

        if (envelope.Payload.GetType() != expected)
        {
            return $"{envelope.Type} must carry a {expected.Name} payload, not {envelope.Payload.GetType().Name}.";
        }

        if (envelope.Actor is not null && (string.IsNullOrWhiteSpace(envelope.Actor.Reference) || string.IsNullOrWhiteSpace(envelope.Actor.Role)))
        {
            return "The event's actor must have both a reference and a role.";
        }

        return null;
    }

    private static JsonSerializerOptions CreatePayloadSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

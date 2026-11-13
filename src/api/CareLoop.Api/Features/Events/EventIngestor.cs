using CareLoop.Contracts.Events;
using CareLoop.Domain.Cases;
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
    /// <item><description>Run the matching <see cref="IEventHandler"/>.</description></item>
    /// <item><description>Build the <see cref="CareLoop.Domain.Events.SourceEvent"/> (applied or rejected, with reason) and call <see cref="ICaseRepository.SaveAsync"/>, which also writes the audit record and outbox messages.</description></item>
    /// </list>
    /// A refused event (for example closure without evidence) is still saved, so the attempt is audited.
    /// </remarks>
    public Task<IngestResult> IngestAsync(EventEnvelope envelope, CancellationToken ct)
    {
        // TODO(WS-2): implement validate, deduplicate, append, apply, audit as described above.
        throw new NotImplementedException("WS2: EventIngestor.IngestAsync");
    }
}

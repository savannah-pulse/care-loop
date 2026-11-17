using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Events;

namespace CareLoop.Contracts.Events;

/// <summary>
/// The internal form of one inbound event, handed to the event ingestor.
/// </summary>
/// <remarks>
/// The envelope is internal only: there is no public endpoint that accepts it. FHIR inbound
/// mappers translate a resource into an envelope, and tests build envelopes directly so the
/// engine can be exercised without FHIR. It carries identifiers, references and statuses, never
/// clinical narrative.
/// </remarks>
public sealed record EventEnvelope
{
    /// <summary>Identifier of the system that produced the event.</summary>
    public required string SourceSystem { get; init; }

    /// <summary>The source's identifier for the event: the FHIR resource identifier plus its version or last-updated time, so a resend of the same state is deduplicated.</summary>
    public required string SourceEventId { get; init; }

    /// <summary>The kind of event; selects the handler and must match the runtime type of <see cref="Payload"/>.</summary>
    public required CaseEventType Type { get; init; }

    /// <summary>The source system's identifier of the actionable result the event belongs to; used to find the case.</summary>
    public required string SourceResultId { get; init; }

    /// <summary>When the action happened according to the source.</summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>Who performed the action at the source, when the source reported it.</summary>
    public Actor? Actor { get; init; }

    /// <summary>
    /// Who actually sent the event when it is not <see cref="SourceSystem"/> itself: CareLoop, or a text message or
    /// email channel (<see cref="Domain.Events.EventOrigins"/>). <see langword="null"/> for an event from the source system.
    /// </summary>
    public string? Origin { get; init; }

    /// <summary>The event-specific data.</summary>
    public required EventPayload Payload { get; init; }
}

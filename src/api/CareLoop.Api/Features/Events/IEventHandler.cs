using CareLoop.Contracts.Events;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;

namespace CareLoop.Api.Features.Events;

/// <summary>
/// Applies one kind of inbound event to a case.
/// </summary>
/// <remarks>
/// There is exactly one handler per source <see cref="CaseEventType"/>, and the event ingestor picks it
/// by <see cref="EventType"/>. A handler only asks the Domain rules and calls the methods on
/// <see cref="Case"/>; it must not persist anything, because the ingestor saves the result through
/// the single write path.
/// </remarks>
public interface IEventHandler
{
    /// <summary>The event type this handler applies.</summary>
    CaseEventType EventType { get; }

    /// <summary>
    /// Applies the event to the case, or refuses it with a reason.
    /// </summary>
    /// <param name="existing">The case the event belongs to, or <see langword="null"/> when none exists yet (only valid for a screening result).</param>
    /// <param name="envelope">The validated, deduplicated event.</param>
    /// <param name="ct">Cancels the operation.</param>
    /// <returns>The resulting case and whether the event was applied or refused.</returns>
    /// <exception cref="InvalidOperationException">The envelope's payload type does not match <see cref="EventType"/>, or <paramref name="existing"/> is <see langword="null"/> for an event that needs a case.</exception>
    Task<EventHandlingResult> HandleAsync(Case? existing, EventEnvelope envelope, CancellationToken ct);
}

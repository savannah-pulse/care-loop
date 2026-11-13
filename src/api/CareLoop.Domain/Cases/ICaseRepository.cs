using CareLoop.Domain.Events;

namespace CareLoop.Domain.Cases;

/// <summary>
/// The single write path for cases.
/// </summary>
/// <remarks>
/// Every change to a case goes through <see cref="SaveAsync"/> so that the source event, the case
/// projection, the audit record and the outbox messages are stored together or not at all. This
/// is what guarantees that no state change is unaudited and no outbound message is lost. Read
/// slices do not use this interface; they query the database directly with no-tracking projections.
/// </remarks>
public interface ICaseRepository
{
    /// <summary>
    /// Loads a case for modification.
    /// </summary>
    /// <param name="caseId">CareLoop's identifier of the case.</param>
    /// <param name="ct">Cancels the operation.</param>
    /// <returns>The case, or <see langword="null"/> when no case has that identifier.</returns>
    Task<Case?> GetAsync(Guid caseId, CancellationToken ct);

    /// <summary>
    /// Finds the case opened for a specific source result, which is how later events are correlated to it.
    /// </summary>
    /// <param name="sourceSystem">Identifier of the system that produced the result.</param>
    /// <param name="sourceResultId">The source system's identifier of the actionable result.</param>
    /// <param name="ct">Cancels the operation.</param>
    /// <returns>The case, or <see langword="null"/> when no case exists for that result.</returns>
    Task<Case?> FindBySourceAsync(string sourceSystem, string sourceResultId, CancellationToken ct);

    /// <summary>
    /// Tells whether a source event has already been stored, so a resend can be ignored.
    /// </summary>
    /// <param name="sourceSystem">Identifier of the system that produced the event.</param>
    /// <param name="sourceEventId">The source's identifier of the event.</param>
    /// <param name="ct">Cancels the operation.</param>
    /// <returns><see langword="true"/> when an event with that source system and identifier exists.</returns>
    Task<bool> EventExistsAsync(string sourceSystem, string sourceEventId, CancellationToken ct);

    /// <summary>
    /// Persists the event, the updated case projection, the audit record
    /// and any raised domain events (outbox) in one transaction.
    /// </summary>
    /// <param name="case">The case after the event was applied, or unchanged when the event was refused.</param>
    /// <param name="appliedEvent">The source event, including whether it was applied or rejected and why.</param>
    /// <param name="ct">Cancels the operation.</param>
    /// <returns>A task that completes when the transaction has committed.</returns>
    /// <remarks>
    /// The implementation builds the <see cref="AuditEvent"/> from the projection as it was loaded
    /// and as it is now, writes one outbox message per event in <see cref="Case.DomainEvents"/>, and
    /// then clears them. A duplicate source event must fail on the unique index rather than be stored twice.
    /// </remarks>
    /// <exception cref="InvalidOperationException">An event with the same source system and source event identifier is already stored.</exception>
    Task SaveAsync(Case @case, SourceEvent appliedEvent, CancellationToken ct);
}

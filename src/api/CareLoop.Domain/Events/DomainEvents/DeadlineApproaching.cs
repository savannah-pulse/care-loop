using CareLoop.Domain.Cases;
using CareLoop.Domain.Recipients;

namespace CareLoop.Domain.Events.DomainEvents;

/// <summary>
/// The current next action is close to its deadline.
/// </summary>
/// <remarks>
/// Notified to the current owner only; nothing is published over FHIR. It is a reminder and changes neither ownership nor the deadline.
/// </remarks>
public sealed record DeadlineApproaching : IDomainEvent
{
    /// <inheritdoc/>
    public required Guid EventId { get; init; }

    /// <inheritdoc/>
    public required Guid CaseId { get; init; }

    /// <inheritdoc/>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>The owner accountable for the next action.</summary>
    public required CaseOwner CurrentOwner { get; init; }

    /// <summary>Key of the next action that is due.</summary>
    public required string NextAction { get; init; }

    /// <summary>When the next action is due.</summary>
    public required DateTimeOffset DueAt { get; init; }
}

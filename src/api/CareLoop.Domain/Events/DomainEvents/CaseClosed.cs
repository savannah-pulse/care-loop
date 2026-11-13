using CareLoop.Domain.Cases;
using CareLoop.Domain.Recipients;

namespace CareLoop.Domain.Events.DomainEvents;

/// <summary>
/// An authorized person closed the case with evidence or a documented exception.
/// </summary>
/// <remarks>
/// Published as the case <c>Task</c> set to completed. No direct notification is sent. Raised only after <see cref="Rules.ClosureGate"/> allowed the closure.
/// </remarks>
public sealed record CaseClosed : IDomainEvent
{
    /// <inheritdoc/>
    public required Guid EventId { get; init; }

    /// <inheritdoc/>
    public required Guid CaseId { get; init; }

    /// <inheritdoc/>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>The typed reason for closure.</summary>
    public required ClosureReason Reason { get; init; }

    /// <summary>The person who authorized the closure.</summary>
    public required Actor AuthorizedBy { get; init; }
}

using CareLoop.Domain.Cases;
using CareLoop.Domain.Recipients;

namespace CareLoop.Domain.Events.DomainEvents;

/// <summary>
/// A proposed owner refused to take the case.
/// </summary>
/// <remarks>
/// Published as a FHIR <c>Task</c> update and notified to the current owner, to whom the next action returns. A rejection never parks a case.
/// </remarks>
public sealed record HandoffRejected : IDomainEvent
{
    /// <inheritdoc/>
    public required Guid EventId { get; init; }

    /// <inheritdoc/>
    public required Guid CaseId { get; init; }

    /// <inheritdoc/>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>The owner who keeps the case and must act next.</summary>
    public required CaseOwner CurrentOwner { get; init; }

    /// <summary>The owner who refused the handoff.</summary>
    public required CaseOwner RejectedBy { get; init; }

    /// <summary>The reason given for the refusal.</summary>
    public required string Reason { get; init; }
}

using CareLoop.Domain.Cases;
using CareLoop.Domain.Recipients;

namespace CareLoop.Domain.Events.DomainEvents;

/// <summary>
/// Ownership of the case was proposed to a new owner.
/// </summary>
/// <remarks>
/// Published as a FHIR <c>Task</c> plus <c>DocumentReference</c> and notified to the proposed owner. The previous owner stays accountable until the proposal is accepted, so a handoff can never leave a case without an owner.
/// </remarks>
public sealed record HandoffProposed : IDomainEvent
{
    /// <inheritdoc/>
    public required Guid EventId { get; init; }

    /// <inheritdoc/>
    public required Guid CaseId { get; init; }

    /// <inheritdoc/>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>The owner who remains accountable until acceptance.</summary>
    public required CaseOwner CurrentOwner { get; init; }

    /// <summary>The owner being asked to take the case.</summary>
    public required CaseOwner ProposedOwner { get; init; }

    /// <summary>When the proposed owner must have responded, from the pathway definition.</summary>
    public required DateTimeOffset RespondBy { get; init; }
}

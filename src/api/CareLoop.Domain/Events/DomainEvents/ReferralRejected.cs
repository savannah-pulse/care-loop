using CareLoop.Domain.Cases;
using CareLoop.Domain.Recipients;

namespace CareLoop.Domain.Events.DomainEvents;

/// <summary>
/// The receiving service rejected the referral.
/// </summary>
/// <remarks>
/// Published as a FHIR <c>Task</c> update and notified to the current owner, who receives a new next action and deadline. A rejected referral never parks a case.
/// </remarks>
public sealed record ReferralRejected : IDomainEvent
{
    /// <inheritdoc/>
    public required Guid EventId { get; init; }

    /// <inheritdoc/>
    public required Guid CaseId { get; init; }

    /// <inheritdoc/>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>The owner who must arrange the alternative.</summary>
    public required CaseOwner CurrentOwner { get; init; }

    /// <summary>The reason given by the receiving service.</summary>
    public required string Reason { get; init; }
}

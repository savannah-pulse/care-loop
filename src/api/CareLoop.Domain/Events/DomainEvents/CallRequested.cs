using CareLoop.Domain.Cases;
using CareLoop.Domain.Recipients;

namespace CareLoop.Domain.Events.DomainEvents;

/// <summary>
/// The patient asked to be called.
/// </summary>
/// <remarks>
/// Published as a FHIR <c>Task</c> and notified to the current owner. A call request is a next action with an owner and a deadline; it is not an acknowledgement of the result.
/// </remarks>
public sealed record CallRequested : IDomainEvent
{
    /// <inheritdoc/>
    public required Guid EventId { get; init; }

    /// <inheritdoc/>
    public required Guid CaseId { get; init; }

    /// <inheritdoc/>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>The owner who must make the call.</summary>
    public required CaseOwner CurrentOwner { get; init; }

    /// <summary>When the call must have happened, from the pathway definition.</summary>
    public required DateTimeOffset DueAt { get; init; }
}

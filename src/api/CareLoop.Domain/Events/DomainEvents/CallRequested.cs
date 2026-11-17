using CareLoop.Domain.Cases;
using CareLoop.Domain.Recipients;

namespace CareLoop.Domain.Events.DomainEvents;

/// <summary>
/// The patient asked to be called.
/// </summary>
/// <remarks>
/// Published as a FHIR <c>Task</c>. A call request is a next action with an owner and a deadline; it is not an acknowledgement of
/// the result. The notice goes to the role the pathway names for the call (<see cref="CallerRole"/>), normally the nurse
/// navigator, because they make the call; it falls back to the current owner when the pathway names none. Telling
/// someone never transfers ownership: the owner stays accountable.
/// </remarks>
public sealed record CallRequested : IDomainEvent
{
    /// <inheritdoc/>
    public required Guid EventId { get; init; }

    /// <inheritdoc/>
    public required Guid CaseId { get; init; }

    /// <inheritdoc/>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>The owner accountable for the case while the call is pending.</summary>
    public required CaseOwner CurrentOwner { get; init; }

    /// <summary>
    /// The role the pathway names for the call-back action, who is told about it; <see langword="null"/> when the pathway
    /// names none, and then the current owner is told.
    /// </summary>
    public string? CallerRole { get; init; }

    /// <summary>When the call must have happened, from the pathway definition.</summary>
    public required DateTimeOffset DueAt { get; init; }
}

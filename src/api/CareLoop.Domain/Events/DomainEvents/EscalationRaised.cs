using CareLoop.Domain.Cases;
using CareLoop.Domain.Recipients;

namespace CareLoop.Domain.Events.DomainEvents;

/// <summary>
/// A deadline passed without the required action and the case was escalated.
/// </summary>
/// <remarks>
/// Published as a FHIR <c>Task</c> and notified to the facility lead, then the coordinator, following the escalation ladder in the pathway definition. Escalation adds oversight; it does not transfer ownership.
/// </remarks>
public sealed record EscalationRaised : IDomainEvent
{
    /// <inheritdoc/>
    public required Guid EventId { get; init; }

    /// <inheritdoc/>
    public required Guid CaseId { get; init; }

    /// <inheritdoc/>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// The <see cref="Escalation.Id"/> of the escalation this event announces; the identifier a person names when
    /// acknowledging it. <see cref="Guid.Empty"/> only on events built outside <see cref="Case.Escalate"/>.
    /// </summary>
    public Guid EscalationId { get; init; }

    /// <summary>The step on the escalation ladder that was reached (1 is the first step).</summary>
    public required int Level { get; init; }

    /// <summary>The role or queue the escalation is addressed to.</summary>
    public required CaseOwner EscalatedTo { get; init; }

    /// <summary>The owner who remains accountable for the overdue action.</summary>
    public required CaseOwner CurrentOwner { get; init; }

    /// <summary>Why the escalation was raised, for example which deadline passed.</summary>
    public required string Reason { get; init; }

    /// <summary>
    /// The role of the person who acknowledged the previous escalation level, for example <c>facility-lead</c>;
    /// <see langword="null"/> at level 1, or when nobody acknowledged the previous level.
    /// </summary>
    /// <remarks>
    /// Carried so the next role up the ladder can see whether the level below has already looked
    /// (C15). Acknowledging never stops the clock, so an acknowledged escalation can still be
    /// followed by the next level. Only the role is carried, never a person's name or any patient detail,
    /// because the event feeds staff notices.
    /// </remarks>
    public string? AcknowledgedByRole { get; init; }

    /// <summary>
    /// When the previous escalation level was acknowledged; <see langword="null"/> whenever
    /// <see cref="AcknowledgedByRole"/> is.
    /// </summary>
    public DateTimeOffset? AcknowledgedAt { get; init; }
}

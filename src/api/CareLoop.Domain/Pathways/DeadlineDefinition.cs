namespace CareLoop.Domain.Pathways;

/// <summary>
/// How long one kind of next action may take, and who is expected to hold the case while it is open.
/// </summary>
/// <remarks>
/// Deadlines are configuration and are placeholders until <see cref="ClinicallyApproved"/> is
/// <see langword="true"/>. No rule may contain a duration of its own.
/// </remarks>
public sealed record DeadlineDefinition
{
    /// <summary>Key of the next action the deadline applies to, for example <c>acknowledge-notification</c>.</summary>
    public required string NextAction { get; init; }

    /// <summary>The urgency code the deadline applies to, or <see langword="null"/> when it applies to every urgency.</summary>
    public string? UrgencyCode { get; init; }

    /// <summary>The time allowed from when the next action is set.</summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>How long before the deadline the owner is warned.</summary>
    public required TimeSpan WarnBefore { get; init; }

    /// <summary><see langword="true"/> once a clinical adviser has approved the value; <see langword="false"/> marks a placeholder.</summary>
    public required bool ClinicallyApproved { get; init; }

    /// <summary>
    /// Role key of the person expected to own the case while this next action is open, for example
    /// <c>nurse-navigator</c> for acknowledging a notification, or <see langword="null"/> when the
    /// pathway does not say.
    /// </summary>
    /// <remarks>
    /// The role is shown on the programme screen and, together with the stage roles, defines which
    /// roles <see cref="Rules.OwnershipRules"/> lets hold a case. It does not restrict who may
    /// perform the action: that is the stage's <see cref="StageDefinition.AllowedRoles"/>.
    /// </remarks>
    public string? OwnerRole { get; init; }

    /// <summary>The clinical or policy source the deadline is taken from, or <see langword="null"/> while it is a placeholder with no source.</summary>
    public string? Citation { get; init; }
}

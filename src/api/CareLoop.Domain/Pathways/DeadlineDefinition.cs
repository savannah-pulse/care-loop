namespace CareLoop.Domain.Pathways;

/// <summary>
/// How long one kind of next action may take.
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
}

namespace CareLoop.Domain.Pathways;

/// <summary>
/// One step on a pathway's escalation ladder.
/// </summary>
public sealed record EscalationStep
{
    /// <summary>Position on the ladder; 1 is the first step.</summary>
    public required int Level { get; init; }

    /// <summary>Role key of who is told at this step, for example the facility lead and then the coordinator.</summary>
    public required string EscalateToRole { get; init; }

    /// <summary>How long after the deadline passed this step is reached.</summary>
    public required TimeSpan AfterOverdue { get; init; }

    /// <summary><see langword="true"/> once a clinical adviser has approved the value; <see langword="false"/> marks a placeholder.</summary>
    public required bool ClinicallyApproved { get; init; }
}

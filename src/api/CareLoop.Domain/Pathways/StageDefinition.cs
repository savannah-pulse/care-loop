using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;

namespace CareLoop.Domain.Pathways;

/// <summary>
/// Entry and exit rules for one pathway stage.
/// </summary>
/// <remarks>
/// These rules are a proposal still to be confirmed by the team, which is why they are data
/// rather than code.
/// </remarks>
public sealed record StageDefinition
{
    /// <summary>The stage being defined.</summary>
    public required CaseStage Stage { get; init; }

    /// <summary>The event types that move a case out of this stage.</summary>
    public required IReadOnlyList<CaseEventType> LeavesOn { get; init; }

    /// <summary>The stage entered when one of <see cref="LeavesOn"/> is applied; <see langword="null"/> for the final stage.</summary>
    public CaseStage? Next { get; init; }

    /// <summary>The role keys allowed to perform the event that leaves this stage.</summary>
    public required IReadOnlyList<string> AllowedRoles { get; init; }

    /// <summary>Key of the next action set when the stage is entered; matches a <see cref="DeadlineDefinition.NextAction"/>.</summary>
    public string? NextActionOnEntry { get; init; }
}

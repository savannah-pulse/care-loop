using CareLoop.Domain.Cases;

namespace CareLoop.Domain.Rules;

/// <summary>
/// The verdict of <see cref="TransitionRules"/> for one event.
/// </summary>
public sealed record TransitionResult
{
    /// <summary>Whether the event may be applied, and if not, why.</summary>
    public required RuleResult Verdict { get; init; }

    /// <summary>The stage the case enters when the event is applied; <see langword="null"/> when the event does not change the stage or is refused.</summary>
    public CaseStage? NextStage { get; init; }

    /// <summary>Key of the next action to set after the event, from the pathway definition; <see langword="null"/> when unchanged.</summary>
    public string? NextAction { get; init; }
}

namespace HisSimulator.Scenarios;

/// <summary>
/// What journey and end-to-end tests assert after a scenario has run (<c>$defs/expectation</c> in the schema).
/// </summary>
/// <remarks>
/// The simulator does not evaluate these; they describe CareLoop's expected state and are carried
/// so the scenario runner screen can show what a run is meant to demonstrate.
/// </remarks>
public sealed record ScenarioExpectation
{
    /// <summary>Expected final CareLoop stage name, for example <c>Closed</c> or <c>FollowUpActive</c>.</summary>
    public string? FinalStage { get; init; }

    /// <summary>Whether the case is expected to be flagged as escalated.</summary>
    public bool? Escalated { get; init; }

    /// <summary>Exception keys expected to be raised on the case.</summary>
    public IReadOnlyList<string>? Exceptions { get; init; }

    /// <summary>Whether CareLoop is expected to accept the scenario's closure attempt.</summary>
    public bool? ClosureAccepted { get; init; }
}

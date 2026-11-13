namespace PatientChannelSimulator.Scenarios;

/// <summary>
/// What tests assert after a scenario has run (<c>$defs/expectation</c> in the schema).
/// </summary>
/// <remarks>
/// The simulator does not evaluate expectations; it carries them so the file round-trips and so
/// the UI can show what the scenario is meant to demonstrate.
/// </remarks>
public sealed record ScenarioExpectation
{
    /// <summary>The CareLoop stage the case should end in.</summary>
    public string? FinalStage { get; init; }

    /// <summary>Whether the case should end with the escalated flag set.</summary>
    public bool? Escalated { get; init; }

    /// <summary>Exception flags the case should carry, for example <c>Unacknowledged</c>.</summary>
    public IReadOnlyList<string>? Exceptions { get; init; }

    /// <summary>Whether CareLoop should have accepted the closure attempt.</summary>
    public bool? ClosureAccepted { get; init; }
}

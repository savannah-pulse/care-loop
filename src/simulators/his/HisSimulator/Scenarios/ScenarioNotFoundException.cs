namespace HisSimulator.Scenarios;

/// <summary>
/// Thrown when a scenario is requested by a name that has no file in the scenarios directory.
/// </summary>
public sealed class ScenarioNotFoundException : Exception
{
    /// <summary>
    /// Creates the exception for a missing scenario.
    /// </summary>
    /// <param name="name">The scenario name that was requested.</param>
    public ScenarioNotFoundException(string name)
        : base($"No scenario named '{name}' exists in the scenarios directory.")
    {
        ScenarioName = name;
    }

    /// <summary>The scenario name that was requested and not found.</summary>
    public string ScenarioName { get; }
}

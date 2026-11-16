namespace PatientChannelSimulator.Scenarios;

/// <summary>
/// One scenario file found in the scenario directory: either the scenario it holds or why it cannot be used.
/// </summary>
/// <param name="Name">The scenario key, taken from the file name.</param>
/// <param name="Scenario">The scenario, or <see langword="null"/> when the file cannot be used.</param>
/// <param name="Problem">Why the file cannot be used, or <see langword="null"/> when it can.</param>
public sealed record ScenarioListEntry(string Name, ScenarioDefinition? Scenario, string? Problem);

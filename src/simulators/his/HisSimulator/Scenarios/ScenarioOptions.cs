namespace HisSimulator.Scenarios;

/// <summary>
/// Configuration for locating scenario files; bound from the <c>Scenarios</c> section.
/// </summary>
public sealed class ScenarioOptions
{
    /// <summary>Name of the configuration section these options bind to.</summary>
    public const string SectionName = "Scenarios";

    /// <summary>
    /// Directory holding the shared scenario JSON files (<c>src/simulators/scenarios</c> in the repository).
    /// </summary>
    /// <remarks>
    /// A relative path is resolved against the application's content root. The default points from the
    /// project folder to <c>src/simulators/scenarios</c>; the Docker image sets <c>/app/scenarios</c>.
    /// </remarks>
    public string Directory { get; set; } = "../../scenarios";

    /// <summary>
    /// Pacing of a scenario run: how many real seconds pass for each scenario hour.
    /// </summary>
    /// <remarks>
    /// CareLoop runs on real time, so a scenario that spans days has to be compressed to be shown
    /// live. The default of 2 turns a two-day journey into about a minute and a half. It must equal
    /// the factor CareLoop's demo pathway deadlines were shortened by, and the patient-channel
    /// simulator's setting of the same name, or deadlines will pass at the wrong moment.
    /// </remarks>
    public double RealSecondsPerScenarioHour { get; set; } = 2;
}

namespace MessagingSimulator.Scenarios;

/// <summary>
/// Configuration of the scenario loader, bound from the <c>Scenarios</c> section.
/// </summary>
public sealed class ScenarioOptions
{
    /// <summary>Name of the configuration section these options bind to.</summary>
    public const string SectionName = "Scenarios";

    /// <summary>
    /// Directory holding the shared scenario JSON files. A relative path is resolved against the
    /// application's content root; in Docker it is <c>/app/scenarios</c>.
    /// </summary>
    public string Directory { get; set; } = "../../scenarios";

    /// <summary>
    /// Pacing of a scenario run: how many real seconds pass for each scenario hour.
    /// </summary>
    /// <remarks>
    /// A scenario states a patient's delay in journey time, for example a response one hour after
    /// delivery. CareLoop runs on real time, so the delay is compressed by this figure when a run
    /// puts the behaviour in force: at the default of 2, one hour becomes two seconds. It must
    /// equal the factor CareLoop's demo pathway deadlines were shortened by, and the HIS
    /// simulator's setting of the same name, or a patient will respond on the wrong side of a deadline.
    /// </remarks>
    public double RealSecondsPerScenarioHour { get; set; } = 2;
}

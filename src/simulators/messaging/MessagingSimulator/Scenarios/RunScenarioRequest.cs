namespace MessagingSimulator.Scenarios;

/// <summary>
/// Optional body of <c>POST /scenarios/{name}/run</c>.
/// </summary>
/// <param name="RealSecondsPerScenarioHour">
/// Pacing for this run: how many real seconds stand for one scenario hour when a patient's
/// <c>respondAfter</c> is turned into a real delay. When <see langword="null"/> the configured
/// <see cref="ScenarioOptions.RealSecondsPerScenarioHour"/> is used. Use the same value as the HIS
/// simulator's run of the same scenario.
/// </param>
public sealed record RunScenarioRequest(double? RealSecondsPerScenarioHour = null);

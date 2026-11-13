namespace HisSimulator.Scenarios;

/// <summary>
/// Optional body of <c>POST /scenarios/{name}/run</c>.
/// </summary>
/// <param name="RealSecondsPerScenarioHour">
/// Pacing for this run: how many real seconds to wait for each scenario hour between steps. When
/// <see langword="null"/> the configured <see cref="ScenarioOptions.RealSecondsPerScenarioHour"/> is
/// used. Zero sends the steps back to back, which is only useful when no deadline needs to pass,
/// because CareLoop measures deadlines in real time.
/// </param>
/// <param name="RunKey">
/// Stable key that makes source event identifiers deterministic across runs (so a rerun with the same key
/// is deduplicated by CareLoop). When <see langword="null"/> a new key is generated.
/// </param>
public sealed record ScenarioRunRequest(double? RealSecondsPerScenarioHour, string? RunKey);

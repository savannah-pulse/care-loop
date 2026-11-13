using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Scenarios;

/// <summary>
/// Puts the patient behaviours of a scenario in force and reports how those patients reacted.
/// </summary>
/// <remarks>
/// <para>
/// The runner sends nothing to CareLoop. Starting a run reads <c>patients[].behaviour</c> from the
/// scenario and stores one <see cref="PatientBehaviour"/> per patient. From then on the simulator
/// reacts whenever CareLoop notifies one of those patients: <c>DeliverySimulator</c> reports the
/// delivery outcome and <c>AutomaticResponder</c> sends the patient's response.
/// </para>
/// <para>
/// The hospital-system events of the same scenario are replayed by the HIS simulator. The two are
/// linked only through CareLoop: the patient reacts because CareLoop, having received those
/// events, sent a notification. A scenario whose patient stays silent must stay unacknowledged.
/// </para>
/// </remarks>
public sealed class ScenarioRunner
{
    private readonly ScenarioLoader _loader;
    private readonly SimulatorStore _store;
    private readonly ISimulatorClock _clock;

    /// <summary>
    /// Creates the runner.
    /// </summary>
    /// <param name="loader">Loads the scenario to run.</param>
    /// <param name="store">Holds run state, the inbox and the message log.</param>
    /// <param name="clock">Source of the current time for scheduling and stamping steps.</param>
    public ScenarioRunner(ScenarioLoader loader, SimulatorStore store, ISimulatorClock clock)
    {
        _loader = loader;
        _store = store;
        _clock = clock;
    }

    /// <summary>
    /// Starts a run of the named scenario and returns immediately with its initial state.
    /// </summary>
    /// <param name="scenarioName">Scenario key to run.</param>
    /// <param name="request">Run options (timing and start time).</param>
    /// <param name="ct">Cancels starting the run; an accepted run continues in the background.</param>
    /// <returns>The new run's state, or <see langword="null"/> when no scenario has that name.</returns>
    /// <exception cref="InvalidOperationException">The scenario is a cohort, which this simulator cannot run on its own.</exception>
    public Task<ScenarioRunState?> StartAsync(string scenarioName, RunScenarioRequest request, CancellationToken ct)
    {
        // TODO(WS-8): load the scenario, refuse a cohort, convert each patient's ScenarioPatientBehaviour to a
        // PatientBehaviour (parsing RespondAfter as an ISO 8601 duration and compressing it by the pacing: request
        // value, else ScenarioOptions.RealSecondsPerScenarioHour) and store it with SetBehaviour naming
        // this scenario, then save the run as Running with the patients' identifiers.
        _ = _loader;
        _ = _store;
        _ = _clock;
        throw new NotImplementedException("WS8: ScenarioRunner.StartAsync");
    }

    /// <summary>
    /// Reads the state of a run.
    /// </summary>
    /// <param name="runId">Identifier returned when the run was started.</param>
    /// <returns>The run's current state, or <see langword="null"/> when no run has that identifier.</returns>
    public ScenarioRunState? GetRun(string runId)
    {
        // TODO(WS-8): return _store.FindRun(runId) with Reactions refreshed from _store.GetReactions for its patients.
        throw new NotImplementedException("WS8: ScenarioRunner.GetRun");
    }
}

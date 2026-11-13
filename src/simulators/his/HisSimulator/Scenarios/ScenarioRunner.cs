using HisSimulator.Outbound;
using HisSimulator.State;
using HisSimulator.Time;

namespace HisSimulator.Scenarios;

/// <summary>
/// Replays the steps of a scenario whose actor is <see cref="ScenarioActor.His"/> against CareLoop.
/// </summary>
/// <remarks>
/// <para>
/// For each HIS step the runner maps the <see cref="ScenarioAction"/> to a <see cref="HisEventKind"/>,
/// waits until the step is due, builds an <see cref="OutboundEventRequest"/> stamped with the real
/// time it is sent and hands it to <see cref="OutboundEventSender"/>. A step is due at run start
/// plus its <c>at</c> offset compressed by the pacing (real seconds per scenario hour). The runner
/// really waits, because CareLoop measures deadlines on a real clock: sending a two-day journey
/// in one burst would never let a deadline pass. The runner never plays the patient: how a patient reacts to a
/// notification is declared on the scenario patient and applied by the patient-channel simulator.
/// </para>
/// <para>
/// A 4xx answer from CareLoop is recorded as <see cref="ScenarioStepStatus.Rejected"/> and the run
/// continues: some scenarios (closure without evidence) exist to show CareLoop refusing an event.
/// </para>
/// <para>
/// Registered as a singleton because runs outlive the request that started them. It resolves the
/// transient <see cref="OutboundEventSender"/> from a new scope per run.
/// </para>
/// </remarks>
public sealed class ScenarioRunner
{
    private readonly ScenarioLoader _loader;
    private readonly SimulatorStore _store;
    private readonly ISimulatorClock _clock;
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// Creates the runner.
    /// </summary>
    /// <param name="loader">Loads the scenario to replay.</param>
    /// <param name="store">Holds run state so progress can be polled.</param>
    /// <param name="clock">Supplies the start instant when a run does not specify one.</param>
    /// <param name="scopeFactory">Creates a scope per run to resolve <see cref="OutboundEventSender"/>.</param>
    public ScenarioRunner(
        ScenarioLoader loader,
        SimulatorStore store,
        ISimulatorClock clock,
        IServiceScopeFactory scopeFactory)
    {
        _loader = loader;
        _store = store;
        _clock = clock;
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Starts replaying a scenario and returns immediately with the run's initial state.
    /// </summary>
    /// <param name="scenarioName">Name of the scenario to replay.</param>
    /// <param name="request">Pacing and run key; <see langword="null"/> uses the defaults.</param>
    /// <param name="cancellationToken">Cancels loading the scenario; it does not cancel a run already started.</param>
    /// <returns>The run in <see cref="ScenarioRunStatus.Pending"/> state, with one step result per scenario step.</returns>
    /// <exception cref="ScenarioNotFoundException">No scenario has that name.</exception>
    /// <exception cref="InvalidDataException">The scenario file is invalid.</exception>
    /// <exception cref="NotSupportedException">
    /// The scenario is a cohort and cohort expansion is not available yet.
    /// </exception>
    public Task<ScenarioRun> StartAsync(string scenarioName, ScenarioRunRequest? request, CancellationToken cancellationToken)
    {
        // TODO(WS-7): load the scenario, create a ScenarioRun (Pending) with a step result per step, save it,
        // then execute the HIS steps in order on a background task: parse each ISO 8601 "at" offset
        // (System.Xml.XmlConvert.ToTimeSpan), wait until run start plus the offset scaled by the pacing
        // (request value, else ScenarioOptions.RealSecondsPerScenarioHour), send via OutboundEventSender stamped with the send time, and save progress
        // after every step. Expand cohort scenarios deterministically from cohort.seed.
        throw new NotImplementedException("WS7: ScenarioRunner.StartAsync");
    }

    /// <summary>
    /// Returns the latest state of a run.
    /// </summary>
    /// <param name="runId">Identifier returned by <see cref="StartAsync"/>.</param>
    /// <returns>The run, or <see langword="null"/> if no run has that identifier.</returns>
    public ScenarioRun? GetRun(Guid runId)
    {
        // TODO(WS-7): read the run from SimulatorStore.
        throw new NotImplementedException("WS7: ScenarioRunner.GetRun");
    }
}

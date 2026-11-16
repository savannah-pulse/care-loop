using HisSimulator.Outbound;
using HisSimulator.State;
using HisSimulator.Time;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Xml;

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
public sealed class ScenarioRunner : IHostedService
{
    private readonly ScenarioLoader _loader;
    private readonly SimulatorStore _store;
    private readonly ISimulatorClock _clock;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<ScenarioOptions> _options;
    private readonly ILogger<ScenarioRunner> _logger;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly ConcurrentDictionary<Guid, Task> _tasks = new();
    private readonly ConcurrentDictionary<string, Guid> _activeKeys = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates the runner.
    /// </summary>
    /// <param name="loader">Loads the scenario to replay.</param>
    /// <param name="store">Holds run state so progress can be polled.</param>
    /// <param name="clock">Supplies the start instant when a run does not specify one.</param>
    /// <param name="scopeFactory">Creates a scope per run to resolve <see cref="OutboundEventSender"/>.</param>
    /// <param name="options">The default real-time pacing.</param>
    /// <param name="logger">Records failed or cancelled runs.</param>
    public ScenarioRunner(
        ScenarioLoader loader,
        SimulatorStore store,
        ISimulatorClock clock,
        IServiceScopeFactory scopeFactory, IOptions<ScenarioOptions> options, ILogger<ScenarioRunner> logger)
    {
        _loader = loader;
        _store = store;
        _clock = clock;
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
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
    /// <exception cref="ArgumentException">The supplied pacing, timer range or run key is invalid.</exception>
    /// <exception cref="InvalidOperationException">Another run has the same active key.</exception>
    /// <exception cref="OperationCanceledException">Loading was cancelled or the host is stopping.</exception>
    public async Task<ScenarioRun> StartAsync(string scenarioName, ScenarioRunRequest? request, CancellationToken cancellationToken)
    {
        _shutdown.Token.ThrowIfCancellationRequested();
        var definition = await _loader.LoadAsync(scenarioName, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        _shutdown.Token.ThrowIfCancellationRequested();
        if (definition.Cohort is not null) throw new NotSupportedException("Cohort expansion belongs to WS10 and is not enabled.");
        var pacing = request?.RealSecondsPerScenarioHour ?? _options.Value.RealSecondsPerScenarioHour;
        if (!double.IsFinite(pacing) || pacing < 0) throw new ArgumentException("Pacing must be finite and nonnegative.");
        var key = request?.RunKey ?? Guid.NewGuid().ToString("N");
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A supplied run key cannot be blank.");
        var started = _clock.UtcNow;
        var steps = definition.Steps!.Select(step =>
        {
            var seconds = XmlConvert.ToTimeSpan(step.At).TotalHours * pacing;
            if (!double.IsFinite(seconds) || seconds * 1000 > uint.MaxValue - 1)
                throw new ArgumentException("The paced offset exceeds the supported timer range.");
            return new ScenarioStepResult(step.Id, step.Actor == ScenarioActor.His ? ScenarioStepStatus.Pending : ScenarioStepStatus.Skipped,
                started.AddSeconds(seconds), key + "/" + step.Id, [], [], null);
        }).ToArray();
        var run = new ScenarioRun(Guid.NewGuid(), scenarioName, ScenarioRunStatus.Pending, started, null, steps);
        if (!_activeKeys.TryAdd(key, run.RunId)) throw new InvalidOperationException("A run with this key is already active.");
        _store.SaveRun(run);
        var work = Task.Run(() => ExecuteAsync(definition, run, key, _shutdown.Token), CancellationToken.None);
        _tasks[run.RunId] = work;
        _ = work.ContinueWith(completed =>
        {
            if (completed.IsFaulted)
            {
                _logger.LogError(completed.Exception, "Scenario run {RunId} failed unexpectedly.", run.RunId);
                var latest = _store.GetRun(run.RunId) ?? run;
                _store.SaveRun(latest with { Status = ScenarioRunStatus.Failed, CompletedAt = _clock.UtcNow });
            }
            _activeKeys.TryRemove(key, out _);
            _tasks.TryRemove(run.RunId, out _);
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return run;
    }

    /// <summary>
    /// Returns the latest state of a run.
    /// </summary>
    /// <param name="runId">Identifier returned by <see cref="StartAsync(string, ScenarioRunRequest, CancellationToken)"/>.</param>
    /// <returns>The run, or <see langword="null"/> if no run has that identifier.</returns>
    public ScenarioRun? GetRun(Guid runId)
    {
        return _store.GetRun(runId);
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _shutdown.CancelAsync();
        await Task.WhenAll(_tasks.Values).WaitAsync(cancellationToken);
    }

    private async Task ExecuteAsync(ScenarioDefinition definition, ScenarioRun initial, string key, CancellationToken ct)
    {
        var run = initial with { Status = ScenarioRunStatus.Running };
        _store.SaveRun(run);
        using var scope = _scopeFactory.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<OutboundEventSender>();
        var steps = run.Steps.ToArray();
        for (var i = 0; i < steps.Length; i++)
        {
            var step = definition.Steps![i];
            if (step.Actor != ScenarioActor.His) continue;
            try
            {
                var remaining = steps[i].ScheduledAt - _clock.UtcNow;
                while (remaining > TimeSpan.Zero)
                {
                    await Task.Delay(remaining, ct);
                    remaining = steps[i].ScheduledAt - _clock.UtcNow;
                }
                ct.ThrowIfCancellationRequested();
                var parameters = new Dictionary<string, JsonElement>(step.Params ?? new Dictionary<string, JsonElement>());
                if (parameters.TryGetValue("evidenceFromStep", out var evidence))
                    parameters["evidenceFromStep"] = JsonSerializer.SerializeToElement(key + "/" + evidence.GetString());
                var patient = definition.Patients!.FirstOrDefault(p => p.Ref == step.PatientRef);
                var kind = step.Action switch
                {
                    ScenarioAction.ScreeningResult => HisEventKind.ScreeningResult,
                    ScenarioAction.ClinicalReview => HisEventKind.ClinicalReview,
                    ScenarioAction.ConsultationUpdate => HisEventKind.ConsultationUpdate,
                    ScenarioAction.ReferralUpdate => HisEventKind.ReferralUpdate,
                    ScenarioAction.AppointmentUpdate => HisEventKind.AppointmentUpdate,
                    ScenarioAction.Outcome => HisEventKind.Outcome,
                    ScenarioAction.Closure => HisEventKind.Closure,
                    ScenarioAction.ConsentRecorded => HisEventKind.ConsentRecorded,
                    _ => throw new ArgumentException("Unknown HIS scenario action."),
                };
                var request = new OutboundEventRequest(kind, key + "/" + step.CaseRef, steps[i].SourceEventKey, _clock.UtcNow, step.Status,
                    step.Performer is { } staff ? new ActingStaff(staff.SyntheticId, staff.Role, staff.Facility) : null,
                    patient is null ? null : new SimulatedPatient(patient.SyntheticId, patient.DisplayName, patient.Language),
                    definition.PathwayId, parameters);
                var result = await sender.SendAsync(request, ct);
                var serverFailure = result.Messages.Any(m => m.StatusCode >= 500);
                steps[i] = steps[i] with
                {
                    Status = serverFailure ? ScenarioStepStatus.Failed : result.Accepted ? ScenarioStepStatus.Sent : ScenarioStepStatus.Rejected,
                    HttpStatusCodes = result.Messages.Select(m => m.StatusCode).ToArray(),
                    LogEntryIds = result.Messages.Select(m => m.LogEntryId).ToArray(),
                    Error = serverFailure ? "CareLoop returned a server error; inspect the FHIR log." : null,
                };
                if (kind == HisEventKind.ScreeningResult && result.Accepted && _store.GetCase(request.CaseRef) is { } sourceCase)
                    _store.UpsertCase(sourceCase with { ScenarioRunId = run.RunId });
                run = run with { Steps = steps.ToArray() };
                if (serverFailure)
                {
                    _logger.LogWarning("Scenario run {RunId}, step {StepId} stopped after an upstream server error.", run.RunId, step.Id);
                    _store.SaveRun(run with { Status = ScenarioRunStatus.Failed, CompletedAt = _clock.UtcNow });
                    return;
                }
                _store.SaveRun(run);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                steps[i] = steps[i] with { Status = ScenarioStepStatus.Failed, Error = "The host stopped the scenario run." };
                _logger.LogInformation("Scenario run {RunId} cancelled by host shutdown.", run.RunId);
                _store.SaveRun(run with { Steps = steps.ToArray(), Status = ScenarioRunStatus.Failed, CompletedAt = _clock.UtcNow });
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                steps[i] = steps[i] with { Status = ScenarioStepStatus.Failed, Error = ex.Message };
                _logger.LogWarning(ex, "Scenario run {RunId}, step {StepId} failed.", run.RunId, step.Id);
                _store.SaveRun(run with { Steps = steps.ToArray(), Status = ScenarioRunStatus.Failed, CompletedAt = _clock.UtcNow });
                return;
            }
        }
        _store.SaveRun(run with { Status = ScenarioRunStatus.Completed, CompletedAt = _clock.UtcNow });
    }
}

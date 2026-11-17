using System.Xml;
using Microsoft.Extensions.Options;
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
/// delivery outcome and <c>AutomaticResponder</c> sends the patient's reply. A behaviour is found by
/// the address a message was sent to; the hospital system simulator sends a patient's synthetic
/// identifier as that address until scenario files carry phone numbers and email addresses.
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
    private readonly IOptions<ScenarioOptions> _options;

    /// <summary>
    /// Creates the runner.
    /// </summary>
    /// <param name="loader">Loads the scenario to run.</param>
    /// <param name="store">Holds run state, the patient behaviours and the reactions.</param>
    /// <param name="clock">Source of the run's start time.</param>
    /// <param name="options">Supplies the pacing used when a run does not state its own.</param>
    public ScenarioRunner(ScenarioLoader loader, SimulatorStore store, ISimulatorClock clock, IOptions<ScenarioOptions> options)
    {
        _loader = loader;
        _store = store;
        _clock = clock;
        _options = options;
    }

    /// <summary>
    /// Starts a run of the named scenario and returns immediately with its initial state.
    /// </summary>
    /// <remarks>
    /// Every patient's behaviour is checked before any is put in force, so a scenario with one bad
    /// patient changes nothing.
    /// </remarks>
    /// <param name="scenarioName">Scenario key to run.</param>
    /// <param name="request">Run options (pacing).</param>
    /// <param name="ct">Cancels starting the run.</param>
    /// <returns>The new run's state, or <see langword="null"/> when no scenario has that name.</returns>
    /// <exception cref="ArgumentException">The scenario name is not a valid scenario key.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The pacing is negative or not a number.</exception>
    /// <exception cref="InvalidOperationException">The scenario is a cohort, which this simulator cannot run on its own.</exception>
    /// <exception cref="InvalidDataException">The scenario file is not valid, or one of its patient behaviours breaks a rule.</exception>
    public async Task<ScenarioRunState?> StartAsync(string scenarioName, RunScenarioRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var pacing = request.RealSecondsPerScenarioHour ?? _options.Value.RealSecondsPerScenarioHour;
        if (double.IsNaN(pacing) || double.IsInfinity(pacing) || pacing < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), pacing, "RealSecondsPerScenarioHour must be zero or more.");
        }

        var scenario = await _loader.LoadAsync(scenarioName, ct);
        if (scenario is null)
        {
            return null;
        }

        if (scenario.Cohort is not null)
        {
            throw new InvalidOperationException(
                $"'{scenario.Name}' is a cohort scenario. This simulator puts the behaviours of named patients in force; run the scenarios the cohort draws from instead.");
        }

        var assignments = (scenario.Patients ?? [])
            .Select(patient => new PatientBehaviourAssignment(patient.SyntheticId, ToBehaviour(scenario.Name, patient, pacing), scenario.Name))
            .ToList();

        foreach (var assignment in assignments)
        {
            _store.SetBehaviour(assignment);
        }

        var run = new ScenarioRunState(
            Guid.NewGuid().ToString("N"),
            scenario.Name,
            ScenarioRunStatus.Running,
            _clock.UtcNow,
            CompletedAt: null,
            [.. assignments.Select(a => a.PatientSyntheticId)],
            Reactions: []);
        _store.SaveRun(run);
        return run;
    }

    /// <summary>
    /// Reads the state of a run.
    /// </summary>
    /// <param name="runId">Identifier returned when the run was started.</param>
    /// <returns>
    /// The run's current state, or <see langword="null"/> when no run has that identifier. The
    /// reactions are those of the run's patients since it started. The run is
    /// <see cref="ScenarioRunStatus.Failed"/> as soon as one of them could not be sent or was refused.
    /// </returns>
    public ScenarioRunState? GetRun(string runId)
    {
        var run = _store.FindRun(runId);
        if (run is null)
        {
            return null;
        }

        var reactions = _store.GetReactions(run.PatientSyntheticIds).Where(r => r.At >= run.StartedAt).ToList();
        var firstFailure = reactions.FirstOrDefault(r => !r.Succeeded);
        return run with
        {
            Reactions = reactions,
            Status = firstFailure is null ? ScenarioRunStatus.Running : ScenarioRunStatus.Failed,
            CompletedAt = firstFailure?.At,
        };
    }

    private static PatientBehaviour ToBehaviour(string scenarioName, ScenarioPatient patient, double pacing)
    {
        InvalidDataException Invalid(string reason) =>
            new($"Scenario '{scenarioName}', patient '{patient.Ref}': {reason}");

        if (!PatientBehaviourRules.IsSynthetic(patient.SyntheticId))
        {
            throw Invalid($"the identifier '{patient.SyntheticId}' is not synthetic (it does not start with {PatientBehaviourRules.SyntheticPrefix}).");
        }

        var file = patient.Behaviour;
        var delivery = file.Delivery switch
        {
            ScenarioVocabulary.DeliveryDeliver => DeliveryBehaviour.Deliver,
            ScenarioVocabulary.DeliveryFail => DeliveryBehaviour.Fail,
            ScenarioVocabulary.DeliverySilent => DeliveryBehaviour.StaySilent,
            _ => throw Invalid($"'{file.Delivery}' is not a known delivery value."),
        };
        var response = file.Response switch
        {
            ScenarioVocabulary.ResponseNone => PatientResponseBehaviour.None,
            ScenarioVocabulary.ResponseAcknowledge => PatientResponseBehaviour.Acknowledge,
            ScenarioVocabulary.ResponseCallMe => PatientResponseBehaviour.CallMe,
            ScenarioVocabulary.ResponseUnderstand or ScenarioVocabulary.ResponseRateConfidence => throw Invalid(
                $"'{file.Response}' is not something a patient does by text message or email. A nurse records teach-back and a confidence rating after a call, in CareLoop."),
            _ => throw Invalid($"'{file.Response}' is not a known response value."),
        };

        if (file.Confidence is not null)
        {
            throw Invalid("a patient gives no confidence rating by text message or email. A nurse records it after a call, in CareLoop.");
        }

        TimeSpan journeyDelay;
        try
        {
            journeyDelay = string.IsNullOrWhiteSpace(file.RespondAfter) ? TimeSpan.Zero : XmlConvert.ToTimeSpan(file.RespondAfter);
        }
        catch (FormatException)
        {
            throw Invalid($"'{file.RespondAfter}' is not an ISO 8601 duration.");
        }

        // The scenario states the delay in journey time; CareLoop runs on real time, so the delay
        // is compressed by the pacing (real seconds per scenario hour).
        var realSeconds = journeyDelay.TotalHours * pacing;
        if (!(realSeconds <= PatientBehaviourRules.MaximumRespondAfter.TotalSeconds))
        {
            throw Invalid($"'{file.RespondAfter}' at {pacing} real seconds per scenario hour is longer than {PatientBehaviourRules.MaximumRespondAfter.TotalDays:0} days.");
        }

        var behaviour = new PatientBehaviour(
            delivery,
            response,
            file.FailureReason,
            TimeSpan.FromSeconds(realSeconds),
            file.RespondToNotification ?? 1);

        return PatientBehaviourRules.Check(behaviour) is { } problem ? throw Invalid(problem) : behaviour;
    }
}

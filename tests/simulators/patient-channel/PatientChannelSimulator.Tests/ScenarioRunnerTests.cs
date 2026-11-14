using System.Net;
using System.Net.Http.Json;
using PatientChannelSimulator.Scenarios;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Tests;

/// <summary>
/// The scenario runner: it puts patient behaviours in force and sends nothing itself (WS8).
/// </summary>
public sealed class ScenarioRunnerTests : IDisposable
{
    private readonly SimulatorFactory _simulator = new();

    public void Dispose() => _simulator.Dispose();

    private async Task<ScenarioRunState> RunAsync(string name, object? body = null, SimulatorFactory? simulator = null)
    {
        var response = await (simulator ?? _simulator).CreateClient().PostAsJsonAsync($"/scenarios/{name}/run", body);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ScenarioRunState>(SimulatorFactory.Json))!;
    }

    private async Task<ScenarioRunState> RunStateAsync(string runId, SimulatorFactory? simulator = null) =>
        (await (simulator ?? _simulator).CreateClient().GetFromJsonAsync<ScenarioRunState>($"/scenarios/runs/{runId}", SimulatorFactory.Json))!;

    [Fact]
    public async Task Run_sets_the_behaviour_of_every_scenario_patient()
    {
        var run = await RunAsync("happy-path");

        Assert.Equal(ScenarioRunStatus.Running, run.Status);
        Assert.Equal("happy-path", run.ScenarioName);
        Assert.Equal(["SYN-PT-0001"], run.PatientSyntheticIds);
        var behaviour = Assert.Single(_simulator.Store.GetBehaviours()).Behaviour;
        Assert.Equal(DeliveryBehaviour.Deliver, behaviour.Delivery);
        Assert.Equal(PatientResponseBehaviour.Acknowledge, behaviour.Response);
    }

    [Fact]
    public async Task Run_sends_nothing_to_CareLoop_by_itself()
    {
        var run = await RunAsync("happy-path");
        _simulator.Clock.Advance(TimeSpan.FromDays(7));
        await _simulator.SendDueAsync();

        Assert.Empty(_simulator.CareLoop.Calls);
        Assert.Empty(_simulator.Store.GetInboxMessages());
        Assert.Empty((await RunStateAsync(run.RunId)).Reactions);
    }

    [Fact]
    public async Task Run_compresses_respond_after_by_the_pacing()
    {
        // happy-path: the patient responds PT1H after delivery.
        await RunAsync("happy-path");
        Assert.Equal(TimeSpan.FromSeconds(2), _simulator.Store.GetBehaviour("SYN-PT-0001").RespondAfter);

        await RunAsync("happy-path", new { realSecondsPerScenarioHour = 30 });
        Assert.Equal(TimeSpan.FromSeconds(30), _simulator.Store.GetBehaviour("SYN-PT-0001").RespondAfter);

        await RunAsync("happy-path", new { realSecondsPerScenarioHour = 0 });
        Assert.Equal(TimeSpan.Zero, _simulator.Store.GetBehaviour("SYN-PT-0001").RespondAfter);
    }

    [Fact]
    public async Task Run_with_a_negative_pacing_is_refused()
    {
        var response = await _simulator.CreateClient().PostAsJsonAsync("/scenarios/happy-path/run", new { realSecondsPerScenarioHour = -1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_simulator.Store.GetBehaviours());
    }

    [Fact]
    public async Task Run_names_the_scenario_on_each_behaviour_it_sets()
    {
        await RunAsync("happy-path");

        Assert.All(_simulator.Store.GetBehaviours(), a => Assert.Equal("happy-path", a.ScenarioName));
    }

    [Fact]
    public async Task Run_state_lists_the_reactions_of_its_patients_only()
    {
        await _simulator.NotifyAsync(requestId: "before-the-run");
        _simulator.Clock.Advance(TimeSpan.FromMinutes(1));
        var run = await RunAsync("happy-path", new { realSecondsPerScenarioHour = 0 });
        await _simulator.NotifyAsync(requestId: "scenario-patient", patientId: "SYN-PT-0001");
        await _simulator.NotifyAsync(requestId: "someone-else", patientId: "SYN-PT-0999");

        var state = await RunStateAsync(run.RunId);

        Assert.Equal([PatientReactionKind.Delivered, PatientReactionKind.Acknowledged], state.Reactions.Select(r => r.Reaction));
        Assert.All(state.Reactions, r => Assert.Equal("scenario-patient", r.MessageId));
        Assert.Equal(ScenarioRunStatus.Running, state.Status);
        Assert.Null(state.CompletedAt);
    }

    [Fact]
    public async Task Run_for_a_silent_patient_shows_delivery_and_no_acknowledgement()
    {
        var run = await RunAsync("silent-patient");
        var patientId = Assert.Single(run.PatientSyntheticIds);

        await _simulator.NotifyAsync(patientId: patientId);
        _simulator.Clock.Advance(TimeSpan.FromDays(30));
        await _simulator.SendDueAsync();

        var state = await RunStateAsync(run.RunId);
        Assert.Equal([PatientReactionKind.Delivered, PatientReactionKind.NoResponse], state.Reactions.Select(r => r.Reaction));
        Assert.Equal(["delivery-status"], _simulator.CareLoop.Categories);
        Assert.Equal(ResponseState.None, (await _simulator.InboxMessageAsync()).Response);
    }

    [Fact]
    public async Task Run_is_failed_when_CareLoop_rejects_a_reaction()
    {
        var run = await RunAsync("happy-path", new { realSecondsPerScenarioHour = 0 });
        _simulator.CareLoop.Refuse("acknowledgement", HttpStatusCode.UnprocessableEntity);
        _simulator.Clock.Advance(TimeSpan.FromSeconds(5));

        await _simulator.NotifyAsync();

        var state = await RunStateAsync(run.RunId);
        Assert.Equal(ScenarioRunStatus.Failed, state.Status);
        Assert.Equal(_simulator.Clock.UtcNow, state.CompletedAt);
        Assert.Contains(state.Reactions, r => r is { Reaction: PatientReactionKind.Acknowledged, Succeeded: false, HttpStatusCode: 422 });
    }

    [Fact]
    public async Task Runner_refuses_to_run_a_cohort_scenario()
    {
        var response = await _simulator.CreateClient().PostAsync("/scenarios/cohort/run", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_simulator.Store.GetBehaviours());
    }

    [Fact]
    public async Task Runner_returns_null_for_an_unknown_scenario()
    {
        var client = _simulator.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/scenarios/no-such-scenario/run", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/scenarios/Not%20A%20Key/run", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/scenarios/runs/no-such-run")).StatusCode);
    }

    [Fact]
    public async Task Runner_takes_all_timestamps_from_the_simulator_clock()
    {
        _simulator.Clock.UtcNow = new DateTimeOffset(2031, 5, 6, 7, 8, 9, TimeSpan.Zero);

        var run = await RunAsync("happy-path", new { realSecondsPerScenarioHour = 0 });
        _simulator.Clock.Advance(TimeSpan.FromHours(3));
        await _simulator.NotifyAsync();

        var state = await RunStateAsync(run.RunId);
        Assert.Equal(new DateTimeOffset(2031, 5, 6, 7, 8, 9, TimeSpan.Zero), state.StartedAt);
        Assert.All(state.Reactions, r => Assert.Equal(new DateTimeOffset(2031, 5, 6, 10, 8, 9, TimeSpan.Zero), r.At));
    }

    [Fact]
    public async Task Scenario_list_summarises_every_shared_scenario()
    {
        var scenarios = await _simulator.CreateClient().GetFromJsonAsync<List<ScenarioSummary>>("/scenarios", SimulatorFactory.Json);

        Assert.Equal(["broken-handoff", "cohort", "happy-path", "rejected-referral", "silent-patient"], scenarios!.Select(s => s.Name));
        Assert.All(scenarios!, s => Assert.True(s.Synthetic));
        var cohort = scenarios!.Single(s => s.Name == "cohort");
        Assert.True(cohort.IsCohort);
        Assert.Equal(0, cohort.PatientCount);
        Assert.Equal(1, scenarios!.Single(s => s.Name == "happy-path").PatientCount);
    }

    [Fact]
    public async Task Scenario_patient_who_rates_confidence_rates_without_acknowledging()
    {
        using var files = new TempScenarios()
            .AddScenario("rates-only", """{ "delivery": "deliver", "response": "rate-confidence", "confidence": 2 }""")
            .AddScenario("acknowledges-and-rates", """{ "delivery": "deliver", "response": "acknowledge", "confidence": 5 }""", patientId: "SYN-PT-0002");
        using var simulator = new SimulatorFactory(scenariosDirectory: files.Path);
        await RunAsync("rates-only", simulator: simulator);
        await RunAsync("acknowledges-and-rates", simulator: simulator);

        await simulator.NotifyAsync(requestId: "one", patientId: "SYN-PT-0001");
        await simulator.NotifyAsync(requestId: "two", patientId: "SYN-PT-0002");

        var one = await simulator.InboxMessageAsync("one");
        var two = await simulator.InboxMessageAsync("two");
        Assert.Equal((ResponseState.None, 2), (one.Response, one.Confidence));
        Assert.Equal((ResponseState.Acknowledged, 5), (two.Response, two.Confidence));
    }

    [Theory]
    [InlineData("""{ "delivery": "deliver", "response": "rate-confidence" }""")]
    [InlineData("""{ "delivery": "deliver", "response": "none", "confidence": 3 }""")]
    [InlineData("""{ "delivery": "deliver", "response": "acknowledge", "confidence": 9 }""")]
    [InlineData("""{ "delivery": "fail", "response": "none" }""")]
    [InlineData("""{ "delivery": "silent", "response": "acknowledge" }""")]
    [InlineData("""{ "delivery": "deliver", "response": "wave" }""")]
    [InlineData("""{ "delivery": "deliver", "response": "acknowledge", "respondAfter": "one hour" }""")]
    public async Task Scenario_with_a_behaviour_that_breaks_a_rule_puts_nothing_in_force(string behaviourJson)
    {
        using var files = new TempScenarios().AddScenario("bad", behaviourJson);
        using var simulator = new SimulatorFactory(scenariosDirectory: files.Path);

        var response = await simulator.CreateClient().PostAsync("/scenarios/bad/run", null);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(simulator.Store.GetBehaviours());
    }
}

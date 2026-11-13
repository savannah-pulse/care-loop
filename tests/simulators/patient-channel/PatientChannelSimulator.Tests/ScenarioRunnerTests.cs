namespace PatientChannelSimulator.Tests;

/// <summary>
/// Planned behaviour of the scenario runner: it puts patient behaviours in force and sends nothing itself (WS8).
/// </summary>
public class ScenarioRunnerTests
{
    [Fact(Skip = "WS8: not implemented")]
    public void Run_sets_the_behaviour_of_every_scenario_patient() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Run_sends_nothing_to_CareLoop_by_itself() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Run_compresses_respond_after_by_the_pacing() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Run_names_the_scenario_on_each_behaviour_it_sets() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Run_state_lists_the_reactions_of_its_patients_only() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Run_for_a_silent_patient_shows_delivery_and_no_acknowledgement() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Run_is_failed_when_CareLoop_rejects_a_reaction() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Runner_refuses_to_run_a_cohort_scenario() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Runner_returns_null_for_an_unknown_scenario() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Runner_takes_all_timestamps_from_the_simulator_clock() { }
}

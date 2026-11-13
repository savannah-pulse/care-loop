using System.Text.Json;
using HisSimulator.Scenarios;

namespace HisSimulator.Tests.Scenarios;

/// <summary>
/// Checks that the scenario records mirror <c>simulators/scenarios/scenario.schema.json</c> by
/// deserializing the real shared scenario files. These tests run now; they do not depend on stubs.
/// </summary>
public sealed class ScenarioFilesTests
{
    public static TheoryData<string> ScenarioNames =>
        new() { "happy-path", "silent-patient", "rejected-referral", "broken-handoff", "cohort" };

    [Theory]
    [MemberData(nameof(ScenarioNames))]
    public void Scenario_file_deserializes_and_is_marked_synthetic(string name)
    {
        var scenario = Read(name);

        Assert.Equal(name, scenario.Name);
        Assert.True(scenario.Synthetic);
        Assert.False(string.IsNullOrWhiteSpace(scenario.PathwayId));
        Assert.False(string.IsNullOrWhiteSpace(scenario.Description));
    }

    [Theory]
    [MemberData(nameof(ScenarioNames))]
    public void Scenario_file_is_either_step_based_or_a_cohort(string name)
    {
        var scenario = Read(name);

        var stepBased = scenario.Steps is { Count: > 0 } && scenario.Patients is { Count: > 0 };
        var cohort = scenario.Cohort is not null;

        Assert.True(stepBased ^ cohort);
    }

    [Fact]
    public void Happy_path_steps_use_synthetic_identifiers_and_known_actors()
    {
        var scenario = Read("happy-path");

        Assert.All(scenario.Patients!, patient =>
        {
            Assert.StartsWith("SYN-", patient.SyntheticId, StringComparison.Ordinal);
            Assert.StartsWith("Synthetic ", patient.DisplayName, StringComparison.Ordinal);
        });
        Assert.Contains(scenario.Steps!, step => step.Actor == ScenarioActor.His && step.Action == ScenarioAction.ScreeningResult);
        Assert.All(scenario.Steps!, step => Assert.Equal(ScenarioActor.His, step.Actor));
        Assert.All(scenario.Patients!, patient => Assert.NotNull(patient.Behaviour));
        Assert.All(
            scenario.Steps!.Where(step => step.Performer is not null),
            step => Assert.StartsWith("SYN-", step.Performer!.SyntheticId, StringComparison.Ordinal));
    }

    [Fact]
    public void Cohort_file_has_seed_size_and_mix()
    {
        var cohort = Read("cohort").Cohort!;

        Assert.True(cohort.Size >= 1);
        Assert.NotEmpty(cohort.Mix);
        Assert.All(cohort.Mix, entry => Assert.True(entry.Weight > 0));
    }

    private static ScenarioDefinition Read(string name)
    {
        var path = Path.Combine(TestPaths.ScenariosDirectory, name + ".json");
        var scenario = JsonSerializer.Deserialize<ScenarioDefinition>(File.ReadAllText(path), ScenarioJson.Options);
        Assert.NotNull(scenario);
        return scenario;
    }
}

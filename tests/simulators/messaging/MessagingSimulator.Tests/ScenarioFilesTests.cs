using System.Text.Json;
using MessagingSimulator.Scenarios;

namespace MessagingSimulator.Tests;

/// <summary>
/// Real (not skipped) checks that the shared scenario files fit the simulator's scenario records.
/// </summary>
public class ScenarioFilesTests
{
    public static TheoryData<string> ScenarioNames => new()
    {
        "happy-path", "silent-patient", "rejected-referral", "broken-handoff", "cohort",
    };

    [Theory]
    [MemberData(nameof(ScenarioNames))]
    public void Scenario_file_deserializes_and_is_marked_synthetic(string name)
    {
        var scenario = Read(name);

        Assert.Equal(name, scenario.Name);
        Assert.True(scenario.Synthetic);
        Assert.False(string.IsNullOrWhiteSpace(scenario.PathwayId));
        Assert.True(scenario.Steps is not null || scenario.Cohort is not null);
    }

    [Theory]
    [MemberData(nameof(ScenarioNames))]
    public void Scenario_patients_are_visibly_synthetic(string name)
    {
        foreach (var patient in Read(name).Patients ?? [])
        {
            Assert.StartsWith("SYN-", patient.SyntheticId, StringComparison.Ordinal);
            Assert.StartsWith("Synthetic ", patient.DisplayName, StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(ScenarioNames))]
    public void Every_patient_declares_a_behaviour_with_known_values(string name)
    {
        string[] deliveries = [ScenarioVocabulary.DeliveryDeliver, ScenarioVocabulary.DeliveryFail, ScenarioVocabulary.DeliverySilent];
        string[] responses =
        [
            ScenarioVocabulary.ResponseNone,
            ScenarioVocabulary.ResponseAcknowledge,
            ScenarioVocabulary.ResponseUnderstand,
            ScenarioVocabulary.ResponseCallMe,
            ScenarioVocabulary.ResponseRateConfidence,
        ];

        foreach (var patient in Read(name).Patients ?? [])
        {
            Assert.Contains(patient.Behaviour.Delivery, deliveries);
            Assert.Contains(patient.Behaviour.Response, responses);

            if (patient.Behaviour.Delivery == ScenarioVocabulary.DeliveryFail)
            {
                Assert.False(string.IsNullOrWhiteSpace(patient.Behaviour.FailureReason));
            }

            if (patient.Behaviour.Delivery != ScenarioVocabulary.DeliveryDeliver)
            {
                Assert.Equal(ScenarioVocabulary.ResponseNone, patient.Behaviour.Response);
            }

            if (patient.Behaviour.RespondAfter is { } respondAfter)
            {
                Assert.True(System.Xml.XmlConvert.ToTimeSpan(respondAfter) >= TimeSpan.Zero);
            }

            Assert.True((patient.Behaviour.RespondToNotification ?? 1) >= 1);

            if (patient.Behaviour.Response == ScenarioVocabulary.ResponseRateConfidence)
            {
                Assert.NotNull(patient.Behaviour.Confidence);
            }

            if (patient.Behaviour.Confidence is { } confidence)
            {
                Assert.InRange(confidence, 1, 5);
                Assert.NotEqual(ScenarioVocabulary.ResponseNone, patient.Behaviour.Response);
            }
        }
    }

    [Theory]
    [MemberData(nameof(ScenarioNames))]
    public void No_scenario_scripts_the_patient_as_a_step(string name)
    {
        Assert.All(Read(name).Steps ?? [], step => Assert.Equal("his", step.Actor));
    }

    [Fact]
    public void Silent_patient_scenario_delivers_and_never_responds()
    {
        var patient = Assert.Single(Read("silent-patient").Patients ?? []);

        Assert.Equal(ScenarioVocabulary.DeliveryDeliver, patient.Behaviour.Delivery);
        Assert.Equal(ScenarioVocabulary.ResponseNone, patient.Behaviour.Response);
    }

    [Fact]
    public void Happy_path_patient_acknowledges_after_delivery()
    {
        var patient = Assert.Single(Read("happy-path").Patients ?? []);

        Assert.Equal(ScenarioVocabulary.DeliveryDeliver, patient.Behaviour.Delivery);
        Assert.Equal(ScenarioVocabulary.ResponseAcknowledge, patient.Behaviour.Response);
    }

    private static ScenarioDefinition Read(string name)
    {
        var path = Path.Combine(FindScenariosDirectory(), name + ".json");
        var scenario = JsonSerializer.Deserialize<ScenarioDefinition>(File.ReadAllText(path), ScenarioJson.Options);
        return Assert.IsType<ScenarioDefinition>(scenario);
    }

    private static string FindScenariosDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "simulators", "scenarios");
            if (File.Exists(Path.Combine(candidate, "scenario.schema.json")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("Could not find src/simulators/scenarios above " + AppContext.BaseDirectory);
    }
}

using System.Text.Json.Nodes;
using CareLoop.Domain.Cases;

namespace CareLoop.Tests.Domain;

/// <summary>
/// The pathway provider loads and validates pathway definitions.
/// </summary>
public sealed class JsonPathwayProviderTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "careloop-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void Breast_screening_pathway_loads_with_every_stage()
    {
        var provider = DomainTestData.Provider();

        var pathway = provider.Get("breast-screening");

        Assert.Equal("breast-screening", pathway.Id);
        Assert.True(pathway.IsPlaceholder);
        Assert.Equal(Enum.GetValues<CaseStage>().ToHashSet(), pathway.Stages.Select(s => s.Stage).ToHashSet());
        Assert.Equal(Enum.GetValues<ClosureReason>().ToHashSet(), pathway.ClosureRules.Select(r => r.Reason).ToHashSet());
        Assert.Equal("facility-lead", pathway.UnassignedQueueRole);
        Assert.Single(provider.GetAll());
    }

    [Fact]
    public void Definitions_are_cached_after_the_first_load()
    {
        var provider = DomainTestData.Provider();

        Assert.Same(provider.Get("breast-screening"), provider.Get("breast-screening"));
        Assert.Same(provider.Get("breast-screening"), provider.GetAll().Single());
    }

    [Fact]
    public void Demo_folder_loads_the_same_pathway_identifier()
    {
        var provider = DomainTestData.Provider("Infrastructure/Pathways/demo");

        var pathway = provider.Get("breast-screening");

        Assert.EndsWith("-demo", pathway.Version);
        Assert.Equal(TimeSpan.FromSeconds(48), pathway.Deadlines.First(d => d.NextAction == "assign-owner").Duration);
    }

    [Fact]
    public void Unknown_pathway_throws()
    {
        var provider = DomainTestData.Provider();

        var exception = Assert.Throws<KeyNotFoundException>(() => provider.Get("SYN-UNKNOWN-PATHWAY"));

        Assert.Contains("SYN-UNKNOWN-PATHWAY", exception.Message);
        Assert.Contains("breast-screening", exception.Message);
    }

    [Fact]
    public void Definition_missing_a_closure_rule_fails_loudly()
    {
        var definition = LoadRealDefinition();
        var rules = definition["closureRules"]!.AsArray();
        rules.RemoveAt(rules.Count - 1);
        WriteTempDefinition("incomplete.json", definition);

        var exception = Assert.Throws<InvalidOperationException>(() => DomainTestData.Provider(_tempDirectory).GetAll());

        Assert.Contains("closure rule", exception.Message);
        Assert.Contains("Untraceable", exception.Message);
    }

    [Fact]
    public void Definition_missing_a_stage_fails_loudly()
    {
        var definition = LoadRealDefinition();
        var stages = definition["stages"]!.AsArray();
        stages.RemoveAt(2);
        WriteTempDefinition("incomplete.json", definition);

        var exception = Assert.Throws<InvalidOperationException>(() => DomainTestData.Provider(_tempDirectory).Get("breast-screening"));

        Assert.Contains("stage FollowUpActive", exception.Message);
    }

    [Fact]
    public void Definition_naming_a_next_action_without_a_deadline_fails_loudly()
    {
        var definition = LoadRealDefinition();
        definition["stages"]![1]!["nextActionOnEntry"] = "PLACEHOLDER-ACTION-WITHOUT-DEADLINE";
        WriteTempDefinition("incomplete.json", definition);

        var exception = Assert.Throws<InvalidOperationException>(() => DomainTestData.Provider(_tempDirectory).GetAll());

        Assert.Contains("PLACEHOLDER-ACTION-WITHOUT-DEADLINE", exception.Message);
    }

    [Fact]
    public void Duplicate_pathway_identifier_fails_loudly()
    {
        WriteTempDefinition("one.json", LoadRealDefinition());
        WriteTempDefinition("two.json", LoadRealDefinition());

        var exception = Assert.Throws<InvalidOperationException>(() => DomainTestData.Provider(_tempDirectory).GetAll());

        Assert.Contains("twice", exception.Message);
    }

    [Fact]
    public void Malformed_file_fails_loudly()
    {
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(Path.Combine(_tempDirectory, "broken.json"), "{ \"id\": \"broken\", ");

        var exception = Assert.Throws<InvalidOperationException>(() => DomainTestData.Provider(_tempDirectory).GetAll());

        Assert.Contains("malformed", exception.Message);
    }

    [Fact]
    public void Subfolders_are_not_loaded()
    {
        WriteTempDefinition("real.json", LoadRealDefinition());
        var sub = Path.Combine(_tempDirectory, "demo");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "real.json"), "not even json");

        var all = DomainTestData.Provider(_tempDirectory).GetAll();

        Assert.Single(all);
    }

    [Fact]
    public void Missing_folder_throws()
    {
        Assert.Throws<DirectoryNotFoundException>(() => DomainTestData.Provider(Path.Combine(_tempDirectory, "nowhere")).GetAll());
    }

    [Fact]
    public void Durations_are_parsed_from_the_file()
    {
        var pathway = DomainTestData.Provider().Get("breast-screening");

        var assignOwner = pathway.Deadlines.Single(d => d.NextAction == "assign-owner" && d.UrgencyCode is null);
        var secondStep = pathway.EscalationLadder.Single(s => s.Level == 2);

        Assert.Equal(TimeSpan.FromDays(1), assignOwner.Duration);
        Assert.Equal(TimeSpan.FromHours(6), assignOwner.WarnBefore);
        Assert.False(assignOwner.ClinicallyApproved);
        Assert.Equal(TimeSpan.FromDays(1), secondStep.AfterOverdue);
        Assert.Equal("coordinator", secondStep.EscalateToRole);
        Assert.Contains(pathway.Deadlines, d => d.NextAction == "acknowledge-notification" && d.UrgencyCode == "PLACEHOLDER-URGENCY-A");
    }

    [Fact]
    public void Citations_owner_roles_and_change_log_are_parsed_from_the_file()
    {
        var pathway = DomainTestData.Provider().Get("breast-screening");

        var acknowledge = pathway.Deadlines.First(d => d.NextAction == "acknowledge-notification");
        Assert.Equal("nurse-navigator", acknowledge.OwnerRole);
        Assert.Contains("PLACEHOLDER", acknowledge.Citation);
        Assert.All(pathway.ClosureRules, r => Assert.Contains("PLACEHOLDER", r.Citation));
        var entry = Assert.Single(pathway.ChangeLog);
        Assert.Equal(pathway.Version, entry.Version);
        Assert.Equal(new DateOnly(2026, 11, 13), entry.Date);
        Assert.Contains("PLACEHOLDER", entry.Approver);
        Assert.False(string.IsNullOrWhiteSpace(entry.Summary));
    }

    [Fact]
    public void Definition_without_a_consultation_answer_deadline_fails_loudly()
    {
        var definition = LoadRealDefinition();
        var deadlines = definition["deadlines"]!.AsArray();
        var answer = deadlines.First(d => d!["nextAction"]!.GetValue<string>() == "answer-consultation");
        deadlines.Remove(answer);
        WriteTempDefinition("incomplete.json", definition);

        var exception = Assert.Throws<InvalidOperationException>(() => DomainTestData.Provider(_tempDirectory).GetAll());

        Assert.Contains("answer-consultation", exception.Message);
    }

    [Fact]
    public void Change_log_entry_without_an_approver_fails_loudly()
    {
        var definition = LoadRealDefinition();
        definition["changeLog"]![0]!["approver"] = " ";
        WriteTempDefinition("incomplete.json", definition);

        var exception = Assert.Throws<InvalidOperationException>(() => DomainTestData.Provider(_tempDirectory).GetAll());

        Assert.Contains("change log", exception.Message);
    }

    [Fact]
    public void Definition_without_a_change_log_still_loads()
    {
        var definition = LoadRealDefinition();
        definition.AsObject().Remove("changeLog");
        WriteTempDefinition("no-log.json", definition);

        var pathway = DomainTestData.Provider(_tempDirectory).Get("breast-screening");

        Assert.Empty(pathway.ChangeLog);
    }

    private static JsonNode LoadRealDefinition()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Infrastructure", "Pathways", "breast-screening.json");
        return JsonNode.Parse(File.ReadAllText(path))!;
    }

    private void WriteTempDefinition(string fileName, JsonNode definition)
    {
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(Path.Combine(_tempDirectory, fileName), definition.ToJsonString());
    }
}

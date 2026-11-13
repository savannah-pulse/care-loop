using System.Text.Json;

namespace CareLoop.Tests.Domain;

/// <summary>
/// Guards the placeholder status of the pathway file until a clinical adviser approves it.
/// </summary>
public sealed class PathwayFileTests
{
    private static JsonElement Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Infrastructure", "Pathways", "breast-screening.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    [Fact]
    public void Pathway_file_is_marked_as_placeholder()
    {
        var root = Load();

        Assert.True(root.GetProperty("isPlaceholder").GetBoolean());
        Assert.Contains("PLACEHOLDER", root.GetProperty("notice").GetString());
    }

    [Fact]
    public void No_deadline_or_escalation_step_claims_clinical_approval()
    {
        var root = Load();

        Assert.All(root.GetProperty("deadlines").EnumerateArray(), d => Assert.False(d.GetProperty("clinicallyApproved").GetBoolean()));
        Assert.All(root.GetProperty("escalationLadder").EnumerateArray(), s => Assert.False(s.GetProperty("clinicallyApproved").GetBoolean()));
    }

    [Fact]
    public void Every_stage_and_every_closure_reason_has_a_rule()
    {
        var root = Load();

        var stages = root.GetProperty("stages").EnumerateArray().Select(s => s.GetProperty("stage").GetString()!).ToHashSet();
        var reasons = root.GetProperty("closureRules").EnumerateArray().Select(r => r.GetProperty("reason").GetString()!).ToHashSet();

        Assert.Equal(Enum.GetNames<CareLoop.Domain.Cases.CaseStage>().ToHashSet(), stages);
        Assert.Equal(Enum.GetNames<CareLoop.Domain.Cases.ClosureReason>().ToHashSet(), reasons);
    }

    [Fact]
    public void Urgency_codes_are_placeholders()
    {
        var root = Load();

        Assert.All(root.GetProperty("urgencyCodes").EnumerateArray(), c => Assert.StartsWith("PLACEHOLDER-", c.GetString()));
    }
}

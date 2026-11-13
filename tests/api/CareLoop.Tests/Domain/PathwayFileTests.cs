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

    [Fact]
    public void Reply_deadlines_are_per_kind_and_still_placeholders()
    {
        var root = Load();
        var deadlines = root.GetProperty("deadlines").EnumerateArray().ToList();
        var actions = deadlines.Select(d => d.GetProperty("nextAction").GetString()).ToList();

        Assert.Contains("answer-consultation", actions);
        Assert.Contains("respond-to-referral", actions);
        Assert.DoesNotContain("respond-to-handoff", actions);
        Assert.All(deadlines.Where(d => d.GetProperty("nextAction").GetString() is "answer-consultation" or "respond-to-referral"),
            d => Assert.False(d.GetProperty("clinicallyApproved").GetBoolean()));
    }

    [Fact]
    public void Every_deadline_names_an_owner_role_and_a_placeholder_citation()
    {
        var root = Load();

        Assert.All(root.GetProperty("deadlines").EnumerateArray(), d =>
        {
            Assert.False(string.IsNullOrWhiteSpace(d.GetProperty("ownerRole").GetString()));
            Assert.Contains("PLACEHOLDER", d.GetProperty("citation").GetString());
        });
        Assert.All(root.GetProperty("closureRules").EnumerateArray(), r => Assert.Contains("PLACEHOLDER", r.GetProperty("citation").GetString()));
    }

    [Fact]
    public void Change_log_has_an_entry_for_the_current_version_marked_unapproved()
    {
        var root = Load();
        var entries = root.GetProperty("changeLog").EnumerateArray().ToList();

        Assert.NotEmpty(entries);
        var current = entries.Last();
        Assert.Equal(root.GetProperty("version").GetString(), current.GetProperty("version").GetString());
        Assert.Contains("PLACEHOLDER", current.GetProperty("approver").GetString());
        Assert.True(DateOnly.TryParse(current.GetProperty("date").GetString(), out _));
    }

    [Fact]
    public void New_personas_are_allowed_where_the_design_gives_them_the_action()
    {
        var root = Load();
        var stages = root.GetProperty("stages").EnumerateArray().ToDictionary(
            s => s.GetProperty("stage").GetString()!,
            s => s.GetProperty("allowedRoles").EnumerateArray().Select(r => r.GetString()!).ToList());

        Assert.Contains("receiving-clinician", stages["FollowUpActive"]);
        Assert.Contains("lab", stages["FollowUpActive"]);
        Assert.Contains("receiving-clinician", stages["ClosureReview"]);
        Assert.DoesNotContain("nurse-navigator", stages["ClosureReview"]);
        Assert.DoesNotContain("lab", stages["ClosureReview"]);
        Assert.All(root.GetProperty("closureRules").EnumerateArray(), r =>
        {
            var roles = r.GetProperty("authorizedRoles").EnumerateArray().Select(x => x.GetString()).ToList();
            Assert.Contains("receiving-clinician", roles);
            Assert.DoesNotContain("nurse-navigator", roles);
            Assert.DoesNotContain("lab", roles);
        });
        Assert.Contains(root.GetProperty("deadlines").EnumerateArray(), d => d.GetProperty("ownerRole").GetString() == "nurse-navigator");
    }
}

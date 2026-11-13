using System.Text.Json;
using System.Text.Json.Nodes;

namespace CareLoop.Tests.Domain;

/// <summary>
/// The demo pathway is the real pathway with every duration shortened by one fixed factor, and
/// nothing else changed, so a live demo exercises the same rules as the real definition.
/// </summary>
public sealed class DemoPathwayFileTests
{
    /// <summary>
    /// One pathway hour is shown as two real seconds. The simulators pace scenarios with the same
    /// figure (<c>Scenarios:RealSecondsPerScenarioHour</c>); the two must stay equal.
    /// </summary>
    private const int Divisor = 3600 / 2;

    private static readonly string[] DurationProperties = ["duration", "warnBefore", "afterOverdue"];

    private static JsonNode Load(params string[] relativePath)
    {
        var path = Path.Combine([AppContext.BaseDirectory, "Infrastructure", "Pathways", .. relativePath]);
        return JsonNode.Parse(File.ReadAllText(path))!;
    }

    [Fact]
    public void Demo_pathway_is_marked_as_placeholder_and_as_demo_timing()
    {
        var demo = Load("demo", "breast-screening.json");

        Assert.True(demo["isPlaceholder"]!.GetValue<bool>());
        Assert.Contains("DEMO TIMING", demo["notice"]!.GetValue<string>());
        Assert.EndsWith("-demo", demo["version"]!.GetValue<string>());
        Assert.All(demo["deadlines"]!.AsArray(), d => Assert.False(d!["clinicallyApproved"]!.GetValue<bool>()));
    }

    [Fact]
    public void Demo_pathway_has_the_same_identifier_as_the_real_one()
    {
        Assert.Equal(
            Load("breast-screening.json")["id"]!.GetValue<string>(),
            Load("demo", "breast-screening.json")["id"]!.GetValue<string>());
    }

    [Fact]
    public void Every_demo_duration_is_the_real_duration_divided_by_the_fixed_factor()
    {
        var real = Load("breast-screening.json");
        var demo = Load("demo", "breast-screening.json");

        foreach (var section in new[] { "deadlines", "escalationLadder" })
        {
            var realItems = real[section]!.AsArray();
            var demoItems = demo[section]!.AsArray();
            Assert.Equal(realItems.Count, demoItems.Count);

            for (var i = 0; i < realItems.Count; i++)
            {
                foreach (var property in DurationProperties.Where(p => realItems[i]![p] is not null))
                {
                    var realDuration = TimeSpan.Parse(realItems[i]![property]!.GetValue<string>());
                    var demoDuration = TimeSpan.Parse(demoItems[i]![property]!.GetValue<string>());
                    Assert.Equal(realDuration / Divisor, demoDuration);
                }
            }
        }
    }

    [Fact]
    public void Demo_pathway_differs_from_the_real_one_only_in_durations_and_labels()
    {
        var real = Load("breast-screening.json");
        var demo = Load("demo", "breast-screening.json");

        foreach (var node in new[] { real, demo })
        {
            foreach (var label in new[] { "notice", "version", "displayName" })
            {
                node.AsObject().Remove(label);
            }

            foreach (var section in new[] { "deadlines", "escalationLadder" })
            {
                foreach (var item in node[section]!.AsArray())
                {
                    foreach (var property in DurationProperties)
                    {
                        item!.AsObject().Remove(property);
                    }
                }
            }
        }

        Assert.True(JsonNode.DeepEquals(real, demo));
    }
}

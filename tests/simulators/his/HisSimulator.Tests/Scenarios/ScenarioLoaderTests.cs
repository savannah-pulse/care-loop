using HisSimulator.Scenarios;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HisSimulator.Tests.Scenarios;

public sealed class ScenarioLoaderTests
{
    private sealed class HostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "HisSimulator";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
    [Fact] public async Task Shared_scenarios_load_in_name_order_without_schema_and_relative_directory_resolves()
    {
        await using var host = await SimulatorHost.StartAsync();
        using var services = new ServiceCollection().AddSingleton<IHostEnvironment>(new HostEnvironment
        { ContentRootPath = Directory.GetParent(TestPaths.ScenariosDirectory)!.FullName }).BuildServiceProvider();
        var loader = new ScenarioLoader(Options.Create(new ScenarioOptions { Directory = "scenarios" }), services.GetRequiredService<IHostEnvironment>());
        var all = await loader.LoadAllAsync(default);
        Assert.Equal(5, all.Count); Assert.Equal(all.Select(s => s.Name).Order(StringComparer.Ordinal), all.Select(s => s.Name));
        Assert.DoesNotContain(all, s => s.Name == "scenario.schema"); Assert.Equal("happy-path", (await loader.LoadAsync("happy-path", default)).Name);
        await Assert.ThrowsAsync<ScenarioNotFoundException>(() => loader.LoadAsync("unknown", default));
    }
    [Theory] [InlineData("../secret")] [InlineData("C:\\secret")] [InlineData("a/b")]
    public async Task Unsafe_names_are_refused(string name)
    {
        var loader = new ScenarioLoader(Options.Create(new ScenarioOptions { Directory = TestPaths.ScenariosDirectory }), new HostEnvironment());
        await Assert.ThrowsAsync<ArgumentException>(() => loader.LoadAsync(name, default));
    }
    [Theory] [InlineData("flag")] [InlineData("name")] [InlineData("patient")] [InlineData("both")] [InlineData("index")]
    [InlineData("offset")] [InlineData("actor")] [InlineData("evidence")]
    public async Task Invalid_shape_identifiers_behaviour_offsets_and_evidence_are_refused(string error)
    {
        using var folder = new ScenarioFolder();
        switch (error)
        {
            case "flag": folder.Scenario["synthetic"] = false; break;
            case "name": folder.Scenario["name"] = "different"; break;
            case "patient": folder.Scenario["patients"]![0]!["syntheticId"] = "REAL-1"; break;
            case "both": folder.Scenario["cohort"] = System.Text.Json.Nodes.JsonNode.Parse("{\"seed\":1,\"size\":1,\"mix\":[{\"scenario\":\"test\",\"weight\":1}]}"); break;
            case "index": folder.Scenario["patients"]![0]!["behaviour"]!["respondToNotification"] = "not-a-number"; break;
            case "offset": folder.Scenario["steps"]![1]!["at"] = "-PT1H"; break;
            case "actor": folder.Scenario["steps"]![1]!["actor"] = "patient"; break;
            case "evidence": folder.Scenario["steps"]![1]!["params"]!["evidenceFromStep"] = "s1"; break;
        }
        folder.Save();
        var loader = new ScenarioLoader(Options.Create(new ScenarioOptions { Directory = folder.Path }), new HostEnvironment());
        await Assert.ThrowsAsync<InvalidDataException>(() => loader.LoadAsync("test", default));
    }
}

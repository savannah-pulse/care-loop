using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PatientChannelSimulator.Scenarios;

namespace PatientChannelSimulator.Tests;

/// <summary>The scenario loader (WS8).</summary>
public sealed class ScenarioLoaderTests : IDisposable
{
    private const string Acknowledges = """{ "delivery": "deliver", "response": "acknowledge" }""";

    private readonly TempScenarios _files = new();

    public void Dispose() => _files.Dispose();

    private ScenarioLoader Loader(string? directory = null, string? contentRoot = null) =>
        new(
            Options.Create(new ScenarioOptions { Directory = directory ?? _files.Path }),
            new Environment { ContentRootPath = contentRoot ?? AppContext.BaseDirectory });

    [Fact]
    public async Task List_returns_every_scenario_except_the_schema_file()
    {
        _files.AddScenario("zebra", Acknowledges).AddScenario("alpha", Acknowledges).Add("scenario.schema.json", """{ "$schema": "not a scenario" }""").Add("notes.txt", "ignored");

        var scenarios = await Loader().ListAsync(CancellationToken.None);

        Assert.Equal(["alpha", "zebra"], scenarios.Select(s => s.Name));
    }

    [Fact]
    public async Task List_reads_the_shared_scenario_files()
    {
        var scenarios = await Loader(SimulatorFactory.ScenariosDirectory()).ListAsync(CancellationToken.None);

        Assert.Equal(["broken-handoff", "cohort", "happy-path", "rejected-referral", "silent-patient"], scenarios.Select(s => s.Name));
        Assert.All(scenarios, s => Assert.True(s.Synthetic));
    }

    [Fact]
    public async Task List_fails_when_the_directory_does_not_exist() =>
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => Loader(Path.Combine(_files.Root, "missing")).ListAsync(CancellationToken.None));

    [Fact]
    public async Task Load_returns_null_for_an_unknown_scenario() =>
        Assert.Null(await Loader().LoadAsync("no-such-scenario", CancellationToken.None));

    [Theory]
    [InlineData("../happy-path")]
    [InlineData("..")]
    [InlineData("sub/dir")]
    [InlineData("scenario.schema")]
    [InlineData("Happy-Path")]
    [InlineData("")]
    public async Task Load_rejects_a_name_that_could_escape_the_directory(string name)
    {
        _files.AddScenario("happy-path", Acknowledges);

        await Assert.ThrowsAsync<ArgumentException>(() => Loader().LoadAsync(name, CancellationToken.None));
    }

    [Fact]
    public async Task Load_rejects_a_file_that_is_not_marked_synthetic()
    {
        _files.AddScenario("real-data", Acknowledges, synthetic: false);

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => Loader().LoadAsync("real-data", CancellationToken.None));

        Assert.Contains("synthetic", ex.Message, StringComparison.Ordinal);
        await Assert.ThrowsAsync<InvalidDataException>(() => Loader().ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Load_rejects_a_file_whose_name_differs_from_its_file_name()
    {
        _files.AddScenario("renamed", Acknowledges, declaredName: "something-else");

        await Assert.ThrowsAsync<InvalidDataException>(() => Loader().LoadAsync("renamed", CancellationToken.None));
    }

    [Fact]
    public async Task Load_rejects_a_file_that_is_not_scenario_json()
    {
        _files.Add("broken.json", "{ this is not json").Add("incomplete.json", """{ "name": "incomplete", "synthetic": true }""");

        await Assert.ThrowsAsync<InvalidDataException>(() => Loader().LoadAsync("broken", CancellationToken.None));
        await Assert.ThrowsAsync<InvalidDataException>(() => Loader().LoadAsync("incomplete", CancellationToken.None));
    }

    [Fact]
    public async Task Relative_directory_is_resolved_against_the_content_root()
    {
        _files.AddScenario("happy-path", Acknowledges);
        var loader = Loader(directory: "scenarios", contentRoot: _files.Root);

        var scenario = await loader.LoadAsync("happy-path", CancellationToken.None);

        Assert.Equal(Path.GetFullPath(_files.Path), loader.Directory);
        Assert.Equal("happy-path", scenario?.Name);
    }

    private sealed class Environment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";

        public string ApplicationName { get; set; } = "PatientChannelSimulator.Tests";

        public required string ContentRootPath { get; set; }

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

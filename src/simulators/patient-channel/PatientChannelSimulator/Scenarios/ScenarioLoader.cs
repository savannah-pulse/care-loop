using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace PatientChannelSimulator.Scenarios;

/// <summary>
/// Reads the shared scenario JSON files from the configured directory.
/// </summary>
/// <remarks>
/// The files are shared with the HIS simulator and described by <c>scenario.schema.json</c>.
/// The loader refuses any file whose <c>synthetic</c> flag is not <see langword="true"/>,
/// so the simulator can never replay data that is not explicitly marked as simulated.
/// </remarks>
public sealed partial class ScenarioLoader
{
    /// <summary>The schema file that lives beside the scenarios and is not one itself.</summary>
    public const string SchemaFileName = "scenario.schema.json";

    private readonly ScenarioOptions _options;
    private readonly IHostEnvironment _environment;

    /// <summary>
    /// Creates the loader.
    /// </summary>
    /// <param name="options">Scenario configuration supplying the directory.</param>
    /// <param name="environment">Host environment; a relative directory is resolved against its content root.</param>
    public ScenarioLoader(IOptions<ScenarioOptions> options, IHostEnvironment environment)
    {
        _options = options.Value;
        _environment = environment;
    }

    /// <summary>
    /// The directory the scenario files are read from, as an absolute path.
    /// </summary>
    public string Directory =>
        Path.GetFullPath(Path.IsPathRooted(_options.Directory)
            ? _options.Directory
            : Path.Combine(_environment.ContentRootPath, _options.Directory));

    /// <summary>
    /// Lists every scenario in the configured directory.
    /// </summary>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>All scenarios (<c>*.json</c> except <c>scenario.schema.json</c>), ordered by name.</returns>
    /// <exception cref="DirectoryNotFoundException">The configured directory does not exist.</exception>
    /// <exception cref="InvalidDataException">A file is not valid scenario JSON, its name does not match the file name, or it is not marked synthetic.</exception>
    public async Task<IReadOnlyList<ScenarioDefinition>> ListAsync(CancellationToken ct)
    {
        var directory = Directory;
        if (!System.IO.Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"The scenario directory '{directory}' does not exist.");
        }

        var scenarios = new List<ScenarioDefinition>();
        foreach (var path in System.IO.Directory.EnumerateFiles(directory, "*.json"))
        {
            if (string.Equals(Path.GetFileName(path), SchemaFileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            scenarios.Add(await ReadAsync(path, ct));
        }

        return [.. scenarios.OrderBy(s => s.Name, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Loads one scenario by name.
    /// </summary>
    /// <param name="name">Scenario key; the file name without extension. Must match <c>^[a-z0-9-]+$</c> so it can never escape the directory.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>The scenario, or <see langword="null"/> when no file has that name.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not a valid scenario key.</exception>
    /// <exception cref="InvalidDataException">The file is not valid scenario JSON, its name does not match the file name, or it is not marked synthetic.</exception>
    public async Task<ScenarioDefinition?> LoadAsync(string name, CancellationToken ct)
    {
        if (name is null || !ScenarioKey().IsMatch(name))
        {
            throw new ArgumentException("A scenario name may only contain lower-case letters, digits and hyphens.", nameof(name));
        }

        var path = Path.Combine(Directory, name + ".json");
        return File.Exists(path) ? await ReadAsync(path, ct) : null;
    }

    private static async Task<ScenarioDefinition> ReadAsync(string path, CancellationToken ct)
    {
        var fileName = Path.GetFileName(path);
        ScenarioDefinition? scenario;
        try
        {
            await using var stream = File.OpenRead(path);
            scenario = await JsonSerializer.DeserializeAsync<ScenarioDefinition>(stream, ScenarioJson.Options, ct);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"The scenario file '{fileName}' is not valid scenario JSON: {ex.Message}", ex);
        }

        if (scenario is null)
        {
            throw new InvalidDataException($"The scenario file '{fileName}' is empty.");
        }

        if (!scenario.Synthetic)
        {
            throw new InvalidDataException($"The scenario file '{fileName}' is not marked synthetic. The simulator only replays simulated data.");
        }

        if (!string.Equals(scenario.Name, Path.GetFileNameWithoutExtension(path), StringComparison.Ordinal))
        {
            throw new InvalidDataException($"The scenario file '{fileName}' declares the name '{scenario.Name}', which differs from its file name.");
        }

        return scenario;
    }

    [GeneratedRegex("^[a-z0-9-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ScenarioKey();
}

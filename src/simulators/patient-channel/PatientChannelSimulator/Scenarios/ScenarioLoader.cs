using Microsoft.Extensions.Options;

namespace PatientChannelSimulator.Scenarios;

/// <summary>
/// Reads the shared scenario JSON files from the configured directory.
/// </summary>
/// <remarks>
/// The files are shared with the HIS simulator and validated by <c>scenario.schema.json</c>.
/// The loader must refuse any file whose <c>synthetic</c> flag is not <see langword="true"/>,
/// so the simulator can never replay data that is not explicitly marked as simulated.
/// </remarks>
public sealed class ScenarioLoader
{
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
    /// Lists every scenario in the configured directory.
    /// </summary>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>All scenarios (<c>*.json</c> except <c>scenario.schema.json</c>), ordered by name.</returns>
    /// <exception cref="DirectoryNotFoundException">The configured directory does not exist.</exception>
    /// <exception cref="InvalidDataException">A file is not valid scenario JSON or is not marked synthetic.</exception>
    public Task<IReadOnlyList<ScenarioDefinition>> ListAsync(CancellationToken ct)
    {
        // TODO(WS-8): resolve _options.Directory against _environment.ContentRootPath, read each *.json
        // except scenario.schema.json with ScenarioJson.Options, validate and return ordered by name.
        _ = _options;
        _ = _environment;
        throw new NotImplementedException("WS8: ScenarioLoader.ListAsync");
    }

    /// <summary>
    /// Loads one scenario by name.
    /// </summary>
    /// <param name="name">Scenario key; the file name without extension. Must match <c>^[a-z0-9-]+$</c> so it can never escape the directory.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>The scenario, or <see langword="null"/> when no file has that name.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not a valid scenario key.</exception>
    /// <exception cref="InvalidDataException">The file is not valid scenario JSON, its name does not match the file name, or it is not marked synthetic.</exception>
    public Task<ScenarioDefinition?> LoadAsync(string name, CancellationToken ct)
    {
        // TODO(WS-8): validate the name pattern, read {directory}/{name}.json with ScenarioJson.Options,
        // and reject files where Synthetic is false or Name differs from the file name.
        throw new NotImplementedException("WS8: ScenarioLoader.LoadAsync");
    }
}

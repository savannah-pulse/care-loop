using Microsoft.Extensions.Options;

namespace HisSimulator.Scenarios;

/// <summary>
/// Reads scenario files from the directory configured in <see cref="ScenarioOptions.Directory"/>.
/// </summary>
/// <remarks>
/// The scenario files are shared with the patient-channel simulator and CareLoop's journey tests,
/// so this class only reads them. Files are deserialized with <see cref="ScenarioJson.Options"/>;
/// <c>scenario.schema.json</c> itself is not a scenario and must be ignored.
/// </remarks>
public sealed class ScenarioLoader
{
    private readonly IOptions<ScenarioOptions> _options;
    private readonly IHostEnvironment _environment;

    /// <summary>
    /// Creates the loader.
    /// </summary>
    /// <param name="options">Supplies the scenarios directory.</param>
    /// <param name="environment">Supplies the content root that a relative directory is resolved against.</param>
    public ScenarioLoader(IOptions<ScenarioOptions> options, IHostEnvironment environment)
    {
        _options = options;
        _environment = environment;
    }

    /// <summary>
    /// Loads every scenario in the configured directory.
    /// </summary>
    /// <param name="cancellationToken">Cancels the file reads.</param>
    /// <returns>All scenarios ordered by name, both step-based and cohort.</returns>
    /// <exception cref="DirectoryNotFoundException">The configured directory does not exist.</exception>
    /// <exception cref="InvalidDataException">
    /// A file is not valid against the scenario schema, including <c>synthetic</c> not being <see langword="true"/>,
    /// a name that differs from the file name, a synthetic identifier without the <c>SYN-</c> prefix,
    /// or a file that is neither step-based nor a cohort.
    /// </exception>
    public Task<IReadOnlyList<ScenarioDefinition>> LoadAllAsync(CancellationToken cancellationToken)
    {
        // TODO(WS-7): resolve the directory against the content root, read every *.json except
        // scenario.schema.json, deserialize with ScenarioJson.Options, validate (synthetic == true,
        // name == file name, SYN- prefixes, exactly one of steps/cohort) and return ordered by name.
        throw new NotImplementedException("WS7: ScenarioLoader.LoadAllAsync");
    }

    /// <summary>
    /// Loads one scenario by name.
    /// </summary>
    /// <param name="name">Scenario key; equals the file name without extension (lower-case letters, digits and hyphens).</param>
    /// <param name="cancellationToken">Cancels the file read.</param>
    /// <returns>The validated scenario.</returns>
    /// <exception cref="ScenarioNotFoundException">No file exists for <paramref name="name"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> does not match the schema's name pattern. Rejecting it prevents a route value
    /// from being used to read a file outside the scenarios directory.
    /// </exception>
    /// <exception cref="InvalidDataException">The file is not valid against the scenario schema.</exception>
    public Task<ScenarioDefinition> LoadAsync(string name, CancellationToken cancellationToken)
    {
        // TODO(WS-7): validate the name against ^[a-z0-9-]+$, read "<name>.json", deserialize and validate
        // exactly as LoadAllAsync does.
        throw new NotImplementedException("WS7: ScenarioLoader.LoadAsync");
    }
}

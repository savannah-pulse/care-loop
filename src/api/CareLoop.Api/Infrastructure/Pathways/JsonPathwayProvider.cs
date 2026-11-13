using CareLoop.Domain.Pathways;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Infrastructure.Pathways;

/// <summary>
/// Loads pathway definitions from the JSON files in the folder named by <see cref="PathwayOptions.Directory"/>.
/// </summary>
/// <remarks>
/// Will read every <c>*.json</c> file directly in the folder (not its subfolders) on first use, deserialize it to a
/// <see cref="PathwayDefinition"/> (camelCase names, enums as strings, durations as
/// <c>d.hh:mm:ss</c>) and cache it. Must fail loudly on a malformed file, on a duplicate pathway
/// identifier, or on a definition missing a rule for any stage or closure reason, because a
/// silently incomplete pathway would let cases stall without a deadline.
/// </remarks>
public sealed class JsonPathwayProvider : IPathwayProvider
{
    private readonly IHostEnvironment _environment;
    private readonly IOptions<PathwayOptions> _options;

    /// <summary>
    /// Creates the provider. Nothing is loaded until the first call.
    /// </summary>
    /// <param name="environment">Supplies the application's content root.</param>
    /// <param name="options">Names the folder to load from, relative to the content root.</param>
    public JsonPathwayProvider(IHostEnvironment environment, IOptions<PathwayOptions> options)
    {
        _environment = environment;
        _options = options;
    }

    /// <inheritdoc/>
    public PathwayDefinition Get(string pathwayId)
    {
        // TODO(WS-1): load and cache the definitions, then return the one with this identifier.
        throw new NotImplementedException("WS1: JsonPathwayProvider.Get");
    }

    /// <inheritdoc/>
    public IReadOnlyList<PathwayDefinition> GetAll()
    {
        // TODO(WS-1): load and cache the definitions, then return them all.
        throw new NotImplementedException("WS1: JsonPathwayProvider.GetAll");
    }
}

using System.Text.Json;

namespace PatientChannelSimulator.Scenarios;

/// <summary>
/// JSON settings for reading scenario files.
/// </summary>
public static class ScenarioJson
{
    /// <summary>
    /// Serializer options matching the scenario schema: camelCase property names, comments and
    /// trailing commas tolerated.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}

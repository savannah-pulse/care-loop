using System.Text.Json;
using System.Text.Json.Serialization;

namespace HisSimulator.Scenarios;

/// <summary>
/// JSON settings for reading scenario files.
/// </summary>
public static class ScenarioJson
{
    /// <summary>
    /// Serializer options matching the scenario schema: camel-case property names, lower-case kebab
    /// enum values (<c>his</c>, <c>screening-result</c>) and rejection of unknown properties,
    /// mirroring the schema's <c>additionalProperties: false</c>.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower, allowIntegerValues: false) },
    };
}

using System.Text.Json;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace PatientChannelSimulator.Outbound;

/// <summary>
/// The simulator's own FHIR R4 JSON reader and writer, built on the Firely SDK.
/// </summary>
/// <remarks>
/// The simulator shares no code with CareLoop: like a real patient channel it serializes and
/// parses FHIR itself. Parsing is strict, so invalid FHIR is refused.
/// </remarks>
public static class FhirJson
{
    /// <summary>FHIR JSON media type used for request and response bodies.</summary>
    public const string MediaType = "application/fhir+json";

    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);

    /// <summary>
    /// Writes a resource as FHIR JSON.
    /// </summary>
    /// <param name="resource">The resource to write.</param>
    /// <returns>The JSON text.</returns>
    public static string Serialize(Resource resource) => JsonSerializer.Serialize(resource, Options);

    /// <summary>
    /// Parses FHIR JSON into a resource of the expected type.
    /// </summary>
    /// <typeparam name="T">The resource type the caller expects.</typeparam>
    /// <param name="json">The JSON text.</param>
    /// <returns>The parsed resource.</returns>
    /// <exception cref="FormatException">The text is not valid FHIR R4, or is a different resource type.</exception>
    public static T Parse<T>(string json)
        where T : Resource
    {
        Resource? resource;
        try
        {
            resource = JsonSerializer.Deserialize<Resource>(json, Options);
        }
        catch (Exception ex) when (ex is JsonException or DeserializationFailedException or NotSupportedException)
        {
            throw new FormatException($"The body is not a valid FHIR R4 resource: {ex.Message}", ex);
        }

        return resource switch
        {
            T typed => typed,
            null => throw new FormatException("The body is empty."),
            _ => throw new FormatException($"Expected a FHIR {ModelInfo.GetFhirTypeNameForType(typeof(T))}, not {resource.TypeName}."),
        };
    }
}

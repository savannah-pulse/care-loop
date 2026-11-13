using System.Text.Json;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace CareLoop.Api.Features.Fhir;

/// <summary>
/// The one set of serializer options used to read and write FHIR R4 JSON with the Firely SDK.
/// </summary>
public static class FhirJsonCodec
{
    /// <summary>Options that parse and write FHIR R4 resources; parsing is strict, so invalid FHIR fails.</summary>
    public static readonly JsonSerializerOptions Options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);

    /// <summary>
    /// Writes a resource as FHIR JSON.
    /// </summary>
    /// <param name="resource">The resource to write.</param>
    /// <returns>The JSON text.</returns>
    public static string Serialize(Resource resource) => JsonSerializer.Serialize(resource, Options);

    /// <summary>
    /// Parses FHIR JSON into a resource.
    /// </summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The resource.</returns>
    /// <exception cref="DeserializationFailedException">The text is not valid FHIR R4.</exception>
    /// <exception cref="JsonException">The text is not JSON or names no resource type.</exception>
    public static Resource Deserialize(string json) =>
        JsonSerializer.Deserialize<Resource>(json, Options) ?? throw new JsonException("The body is empty.");
}

using Hl7.Fhir.Model;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Infrastructure.Fhir;

/// <summary>
/// Sends FHIR R4 resources to the external systems.
/// </summary>
/// <remarks>
/// Will serialize the resource with the Firely serializer and <c>PUT</c> it to
/// <c>{baseUrl}/fhir/{resourceType}/{id}</c> so that a retried send of the same resource is
/// idempotent. A system is addressed by its identifier in <see cref="ExternalSystemsOptions"/>; there is
/// no fallback, so a resource for an unknown system fails instead of going elsewhere.
/// </remarks>
public sealed class FhirHttpClient
{
    private readonly HttpClient _http;
    private readonly IOptions<ExternalSystemsOptions> _endpoints;

    /// <summary>
    /// Creates the client.
    /// </summary>
    /// <param name="http">The HTTP client supplied by the HTTP client factory.</param>
    /// <param name="endpoints">The configured external systems.</param>
    public FhirHttpClient(HttpClient http, IOptions<ExternalSystemsOptions> endpoints)
    {
        _http = http;
        _endpoints = endpoints;
    }

    /// <summary>
    /// Sends one resource to an external system.
    /// </summary>
    /// <param name="systemId">Identifier of the system to send to; a key of <see cref="ExternalSystemsOptions.Systems"/>.</param>
    /// <param name="resource">The resource to send; it must have a logical identifier.</param>
    /// <param name="ct">Cancels the send.</param>
    /// <returns>A task that completes when the system has accepted the resource.</returns>
    /// <exception cref="InvalidOperationException">No system with that identifier is configured, it has no base URL, or the resource has no identifier.</exception>
    /// <exception cref="HttpRequestException">The system could not be reached or refused the resource.</exception>
    public System.Threading.Tasks.Task SendAsync(string systemId, Resource resource, CancellationToken ct)
    {
        // TODO(WS-3): serialize with the Firely serializer and PUT to the system's base URL as application/fhir+json; fail if the system is not configured.
        throw new NotImplementedException("WS3: FhirHttpClient.SendAsync");
    }
}

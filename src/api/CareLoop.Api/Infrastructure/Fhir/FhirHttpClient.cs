using System.Net.Http.Headers;
using System.Text;
using CareLoop.Api.Features.Fhir;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Infrastructure.Fhir;

/// <summary>
/// Sends FHIR R4 resources to the external systems, and reads the few resources CareLoop has to look up.
/// </summary>
/// <remarks>
/// Serializes the resource with the Firely serializer and <c>PUT</c>s it to
/// <c>{baseUrl}/fhir/{resourceType}/{id}</c> so that a retried send of the same resource is
/// idempotent. A system is addressed by its identifier in <see cref="ExternalSystemsOptions"/>; there is
/// no fallback, so a resource for an unknown system fails instead of going elsewhere.
/// </remarks>
public sealed class FhirHttpClient
{
    private static readonly MediaTypeWithQualityHeaderValue FhirMediaType = new(FhirEndpoints.FhirJson);

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
    public async System.Threading.Tasks.Task SendAsync(string systemId, Resource resource, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (string.IsNullOrWhiteSpace(resource.Id))
        {
            throw new InvalidOperationException($"A {resource.TypeName} without an id cannot be sent: the send would not be idempotent.");
        }

        var baseUrl = BaseUrlOf(systemId)
            ?? throw new InvalidOperationException($"External system '{systemId}' is not configured under {ExternalSystemsOptions.SectionName}:Systems, or has no BaseUrl.");

        using var content = new StringContent(FhirJsonCodec.Serialize(resource), Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue(FhirEndpoints.FhirJson) { CharSet = "utf-8" };

        using var request = new HttpRequestMessage(HttpMethod.Put, ResourceUri(baseUrl, resource.TypeName, resource.Id)) { Content = content };
        request.Headers.Accept.Add(FhirMediaType);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Reads one resource from an external system, for example the <c>Patient</c> a screening result refers to.
    /// </summary>
    /// <param name="systemId">Identifier of the system to read from.</param>
    /// <param name="resourceType">The FHIR resource type.</param>
    /// <param name="id">The logical identifier.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>The resource, or <see langword="null"/> when the system is not configured or does not have it.</returns>
    /// <exception cref="HttpRequestException">The system could not be reached or failed.</exception>
    public async System.Threading.Tasks.Task<Resource?> ReadAsync(string systemId, string resourceType, string id, CancellationToken ct)
    {
        if (BaseUrlOf(systemId) is not { } baseUrl)
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, ResourceUri(baseUrl, resourceType, id));
        request.Headers.Accept.Add(FhirMediaType);

        using var response = await _http.SendAsync(request, ct);
        if (response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Gone)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        return FhirJsonCodec.Deserialize(json);
    }

    /// <summary>
    /// Whether a system is configured with a base URL.
    /// </summary>
    /// <param name="systemId">Identifier of the system.</param>
    /// <returns><see langword="true"/> when CareLoop can address it.</returns>
    public bool IsConfigured(string systemId) => BaseUrlOf(systemId) is not null;

    private string? BaseUrlOf(string systemId) =>
        !string.IsNullOrWhiteSpace(systemId)
        && _endpoints.Value.Systems.TryGetValue(systemId, out var endpoint)
        && !string.IsNullOrWhiteSpace(endpoint.BaseUrl)
            ? endpoint.BaseUrl
            : null;

    private static Uri ResourceUri(string baseUrl, string resourceType, string id) =>
        new($"{baseUrl.TrimEnd('/')}/fhir/{Uri.EscapeDataString(resourceType)}/{Uri.EscapeDataString(id)}");
}

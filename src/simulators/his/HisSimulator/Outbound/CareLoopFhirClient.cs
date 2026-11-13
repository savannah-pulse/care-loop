using HisSimulator.State;
using HisSimulator.Time;
using Hl7.Fhir.Model;
using FhirTask = Hl7.Fhir.Model.Task;

namespace HisSimulator.Outbound;

/// <summary>
/// Typed HTTP client for CareLoop's FHIR R4 boundary: the simulator's only way to reach CareLoop.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="HttpClient"/> base address is <see cref="CareLoopOptions.FhirBaseUrl"/> with a trailing
/// slash, so request URIs are relative resource paths such as <c>DiagnosticReport</c> or <c>Task/SYN-123</c>.
/// </para>
/// <para>
/// Resources are serialized with the Firely SDK as <c>application/fhir+json</c>. Every exchange,
/// successful or not, is appended to the FHIR message log so the ClientApp can show the wire content.
/// A non-success HTTP status is returned to the caller, not thrown: a rejection by CareLoop
/// (for example closure without evidence) is a demonstrable result, not a simulator fault.
/// </para>
/// </remarks>
public sealed class CareLoopFhirClient
{
    private readonly HttpClient _httpClient;
    private readonly SimulatorStore _store;
    private readonly ISimulatorClock _clock;

    /// <summary>
    /// Creates the client.
    /// </summary>
    /// <param name="httpClient">HTTP client whose base address is CareLoop's FHIR base URL.</param>
    /// <param name="store">Receives a log entry for every exchange.</param>
    /// <param name="clock">Timestamps the log entries.</param>
    public CareLoopFhirClient(HttpClient httpClient, SimulatorStore store, ISimulatorClock clock)
    {
        _httpClient = httpClient;
        _store = store;
        _clock = clock;
    }

    /// <summary>
    /// Sends a resource with <c>POST {FhirBaseUrl}/{resourceType}</c>.
    /// </summary>
    /// <param name="resource">The resource to create. It must carry the simulated-data marker set by its builder.</param>
    /// <param name="caseRef">Simulator-side case handle recorded on the log entry, when known.</param>
    /// <param name="cancellationToken">Cancels the HTTP call.</param>
    /// <returns>The HTTP status and the identifier of the log entry for the exchange.</returns>
    /// <exception cref="HttpRequestException">CareLoop could not be reached at all (the failure is still logged).</exception>
    public Task<FhirSendResult> CreateAsync(Resource resource, string? caseRef, CancellationToken cancellationToken)
    {
        // TODO(WS-7): serialize with Firely (FhirJsonSerializer / JsonSerializerOptions.ForFhir), POST to
        // resource.TypeName as application/fhir+json, append a Sent FhirMessageLogEntry, return the status.
        throw new NotImplementedException("WS7: CareLoopFhirClient.CreateAsync");
    }

    /// <summary>
    /// Sends a resource state change with <c>PUT {FhirBaseUrl}/{resourceType}</c>.
    /// </summary>
    /// <remarks>
    /// CareLoop turns FHIR state into events using the resource identifier plus its version (or
    /// last-updated time) as the source event id, so the resource must carry <c>meta.versionId</c>
    /// or <c>meta.lastUpdated</c> reflecting the new state.
    /// </remarks>
    /// <param name="resource">The resource in its new state; its logical id must be set.</param>
    /// <param name="caseRef">Simulator-side case handle recorded on the log entry, when known.</param>
    /// <param name="cancellationToken">Cancels the HTTP call.</param>
    /// <returns>The HTTP status and the identifier of the log entry for the exchange.</returns>
    /// <exception cref="ArgumentException">The resource has no logical id.</exception>
    /// <exception cref="HttpRequestException">CareLoop could not be reached at all (the failure is still logged).</exception>
    public Task<FhirSendResult> UpdateAsync(Resource resource, string? caseRef, CancellationToken cancellationToken)
    {
        // TODO(WS-7): as CreateAsync but with PUT; confirm with WS6 whether the id travels in the URL
        // (PUT /fhir/{type}/{id}) or only in the body (AGENTS.md lists PUT /fhir/{resourceType}).
        throw new NotImplementedException("WS7: CareLoopFhirClient.UpdateAsync");
    }

    /// <summary>
    /// Reads CareLoop's case status with <c>GET {FhirBaseUrl}/Task/{id}</c>.
    /// </summary>
    /// <param name="taskId">Logical id of CareLoop's case <c>Task</c>.</param>
    /// <param name="cancellationToken">Cancels the HTTP call.</param>
    /// <returns>The case <c>Task</c>, or <see langword="null"/> if CareLoop answers 404.</returns>
    /// <exception cref="HttpRequestException">CareLoop could not be reached or answered with another error status.</exception>
    /// <exception cref="FormatException">The response body is not a valid FHIR R4 <c>Task</c>.</exception>
    public Task<FhirTask?> GetTaskAsync(string taskId, CancellationToken cancellationToken)
    {
        // TODO(WS-7): GET Task/{taskId} with Accept application/fhir+json, parse with Firely, log the exchange.
        throw new NotImplementedException("WS7: CareLoopFhirClient.GetTaskAsync");
    }
}

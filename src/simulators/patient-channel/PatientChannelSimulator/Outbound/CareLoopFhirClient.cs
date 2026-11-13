using Hl7.Fhir.Model;
using Microsoft.Extensions.Options;
using PatientChannelSimulator.Scenarios;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Outbound;

/// <summary>
/// Typed HTTP client for everything the simulator sends to or reads from CareLoop.
/// </summary>
/// <remarks>
/// Serializes and parses FHIR itself with the Firely SDK, as a real patient channel would, and
/// records every exchange in the FHIR message log. Case status is read from
/// <see cref="CareLoopOptions.FhirBaseUrl"/>; delivery reports and patient responses are posted
/// to <see cref="CareLoopOptions.NotificationCallbackUrl"/>, the callback of CareLoop's
/// simulator notification provider.
/// </remarks>
public sealed class CareLoopFhirClient
{
    private readonly HttpClient _http;
    private readonly SimulatorStore _store;
    private readonly ISimulatorClock _clock;
    private readonly IOptions<CareLoopOptions> _options;

    /// <summary>
    /// Creates the client.
    /// </summary>
    /// <param name="http">HTTP client whose base address is CareLoop's FHIR base URL.</param>
    /// <param name="store">Receives a log entry for every exchange.</param>
    /// <param name="clock">Source of the timestamps on log entries.</param>
    /// <param name="options">Supplies the notification callback address.</param>
    public CareLoopFhirClient(HttpClient http, SimulatorStore store, ISimulatorClock clock, IOptions<CareLoopOptions> options)
    {
        _http = http;
        _store = store;
        _clock = clock;
        _options = options;
    }

    /// <summary>
    /// Sends a <c>Communication</c> (delivery report or patient response) to CareLoop with
    /// <c>POST {NotificationCallbackUrl}</c>.
    /// </summary>
    /// <param name="communication">The resource to send, produced by one of the builders.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The HTTP outcome. A rejected resource is reported in the result, not thrown.</returns>
    /// <exception cref="HttpRequestException">CareLoop could not be reached at all.</exception>
    public System.Threading.Tasks.Task<FhirSendResult> SendCommunicationAsync(Communication communication, CancellationToken ct)
    {
        // TODO(WS-8): serialize with the Firely JSON serializer, POST as application/fhir+json to
        // CareLoopOptions.NotificationCallbackUrl (an absolute address, not under the FHIR base), append an Outbound FhirMessageLogEntry and map the response to FhirSendResult.
        _ = _http;
        _ = _store;
        _ = _clock;
        _ = _options;
        throw new NotImplementedException("WS8: CareLoopFhirClient.SendCommunicationAsync");
    }

    /// <summary>
    /// Reads the case <c>Task</c> for the patient case-status view with <c>GET {FhirBaseUrl}/Task/{id}</c>.
    /// </summary>
    /// <param name="taskId">Logical id of the case <c>Task</c> in CareLoop.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The parsed <c>Task</c>, or <see langword="null"/> when CareLoop returns 404.</returns>
    /// <exception cref="HttpRequestException">CareLoop could not be reached or returned an unexpected error status.</exception>
    /// <exception cref="FormatException">The response body was not a valid FHIR <c>Task</c>.</exception>
    public System.Threading.Tasks.Task<Hl7.Fhir.Model.Task?> GetTaskAsync(string taskId, CancellationToken ct)
    {
        // TODO(WS-8): GET "Task/{taskId}", append an Outbound FhirMessageLogEntry, parse the body with the
        // Firely JSON parser and return null on 404.
        throw new NotImplementedException("WS8: CareLoopFhirClient.GetTaskAsync");
    }
}

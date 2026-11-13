namespace PatientChannelSimulator.State;

/// <summary>
/// One entry in the simulator's FHIR message log: a single request/response exchange with CareLoop.
/// </summary>
/// <remarks>
/// The log exists so a demo audience can see exactly what crossed the FHIR boundary. Bodies are
/// synthetic FHIR JSON; <paramref name="Simulated"/> is always <see langword="true"/>.
/// </remarks>
/// <param name="EntryId">Simulator-local identifier of the log entry.</param>
/// <param name="At">When the exchange happened, taken from the simulator clock.</param>
/// <param name="Direction">Whether CareLoop called the simulator or the reverse.</param>
/// <param name="Method">HTTP method of the exchange, for example <c>POST</c> or <c>GET</c>.</param>
/// <param name="Url">Request URL or path of the exchange.</param>
/// <param name="ResourceType">FHIR resource type exchanged, for example <c>CommunicationRequest</c>.</param>
/// <param name="ResourceId">Logical id of the resource, when known.</param>
/// <param name="StatusCode">HTTP status code of the response; <see langword="null"/> when the call failed before a response arrived.</param>
/// <param name="Body">The FHIR JSON exchanged (request body for writes, response body for reads); <see langword="null"/> when there was none.</param>
/// <param name="Simulated">Visible simulated-data marker; always <see langword="true"/>.</param>
public sealed record FhirMessageLogEntry(
    string EntryId,
    DateTimeOffset At,
    FhirMessageDirection Direction,
    string Method,
    string Url,
    string ResourceType,
    string? ResourceId,
    int? StatusCode,
    string? Body,
    bool Simulated = true);

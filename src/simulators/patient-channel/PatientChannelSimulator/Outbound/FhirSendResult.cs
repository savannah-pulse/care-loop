namespace PatientChannelSimulator.Outbound;

/// <summary>
/// Outcome of sending one FHIR resource to CareLoop.
/// </summary>
/// <param name="StatusCode">HTTP status code CareLoop returned.</param>
/// <param name="Succeeded">Whether the status code was a success (2xx), including a deduplicated resend.</param>
/// <param name="Location">The <c>Location</c> header of the response, when CareLoop returned one.</param>
/// <param name="ResponseBody">The response body (for example an <c>OperationOutcome</c>), when there was one.</param>
public sealed record FhirSendResult(int StatusCode, bool Succeeded, string? Location, string? ResponseBody);

namespace HisSimulator.State;

/// <summary>
/// One simulated FHIR exchange between the HIS simulator and CareLoop, kept for the message log screen.
/// </summary>
/// <param name="Id">Identifier of this log entry, assigned by the store.</param>
/// <param name="Direction">Whether the simulator sent or received the message.</param>
/// <param name="At">When the exchange happened, from <see cref="Time.ISimulatorClock"/>.</param>
/// <param name="Method">HTTP method used: <c>POST</c>, <c>PUT</c> or <c>GET</c>.</param>
/// <param name="ResourceType">FHIR resource type, for example <c>DiagnosticReport</c> or <c>Task</c>.</param>
/// <param name="ResourceId">Logical id of the resource when known; otherwise <see langword="null"/>.</param>
/// <param name="StatusCode">HTTP status of the exchange; <see langword="null"/> if no response was received.</param>
/// <param name="CaseRef">Simulator-side handle of the case the message belongs to, when it can be correlated.</param>
/// <param name="Body">The FHIR JSON exactly as sent or received, so the log shows the real wire content.</param>
public sealed record FhirMessageLogEntry(
    Guid Id,
    FhirMessageDirection Direction,
    DateTimeOffset At,
    string Method,
    string ResourceType,
    string? ResourceId,
    int? StatusCode,
    string? CaseRef,
    string Body)
{
    /// <summary>
    /// Always <see langword="true"/>: marks the entry as simulated wherever it is displayed or exported.
    /// </summary>
    public bool Synthetic => true;
}

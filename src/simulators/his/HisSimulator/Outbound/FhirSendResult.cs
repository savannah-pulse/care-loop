namespace HisSimulator.Outbound;

/// <summary>
/// The outcome of sending one FHIR resource to CareLoop.
/// </summary>
/// <param name="Method">HTTP method used: <c>POST</c> or <c>PUT</c>.</param>
/// <param name="ResourceType">FHIR resource type that was sent.</param>
/// <param name="ResourceId">Logical id of the resource, as sent or as assigned by CareLoop.</param>
/// <param name="StatusCode">HTTP status CareLoop answered with.</param>
/// <param name="LogEntryId">Identifier of the <see cref="State.FhirMessageLogEntry"/> recording the exchange.</param>
public sealed record FhirSendResult(string Method, string ResourceType, string? ResourceId, int StatusCode, Guid LogEntryId)
{
    /// <summary>
    /// Whether CareLoop accepted the resource (a 2xx status). A duplicate that CareLoop deduplicates still counts as accepted.
    /// </summary>
    public bool Accepted => StatusCode is >= 200 and < 300;

    /// <summary>
    /// Always <see langword="true"/>: the message was a simulated one.
    /// </summary>
    public bool Synthetic => true;
}

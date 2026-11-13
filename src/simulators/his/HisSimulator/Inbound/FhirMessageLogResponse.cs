using HisSimulator.State;

namespace HisSimulator.Inbound;

/// <summary>
/// Body of <c>GET /fhir-log</c>.
/// </summary>
/// <param name="Entries">FHIR messages sent and received by the simulator, newest first.</param>
public sealed record FhirMessageLogResponse(IReadOnlyList<FhirMessageLogEntry> Entries)
{
    /// <summary>
    /// Always <see langword="true"/>: the log holds simulated messages only.
    /// </summary>
    public bool Synthetic => true;
}

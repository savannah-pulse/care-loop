namespace HisSimulator.Outbound;

/// <summary>
/// The outcome of sending one HIS event, which may span several FHIR resources.
/// </summary>
/// <param name="Kind">The kind of event that was sent.</param>
/// <param name="CaseRef">Simulator-side handle of the case the event belongs to.</param>
/// <param name="SourceEventKey">The deterministic key the resource identifiers were derived from.</param>
/// <param name="Messages">One result per FHIR resource sent, in send order.</param>
public sealed record OutboundEventResult(
    HisEventKind Kind,
    string CaseRef,
    string SourceEventKey,
    IReadOnlyList<FhirSendResult> Messages)
{
    /// <summary>
    /// Whether CareLoop accepted every resource of the event.
    /// </summary>
    public bool Accepted => Messages.All(message => message.Accepted);

    /// <summary>
    /// Always <see langword="true"/>: the event was a simulated one.
    /// </summary>
    public bool Synthetic => true;
}

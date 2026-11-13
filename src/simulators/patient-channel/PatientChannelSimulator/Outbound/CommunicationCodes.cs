namespace PatientChannelSimulator.Outbound;

/// <summary>
/// The codes this simulator puts on outbound FHIR <c>Communication</c> resources.
/// </summary>
/// <remarks>
/// <c>Communication.category</c> is how CareLoop tells a transport report from a patient
/// response, and one patient response from another. That distinction is safety-relevant: only an
/// acknowledgement (or teach-back) completes the notification track, a delivery report never does.
/// The simulator shares no code with CareLoop, so these values are PROVISIONAL and must be kept
/// equal to CareLoop's <c>StatusMap</c> by agreement between the workstreams.
/// </remarks>
public static class CommunicationCodes
{
    /// <summary>Code system of the category codes below (provisional, non-resolvable).</summary>
    public const string CategorySystem = "https://careloop.invalid/fhir/CodeSystem/communication-category";

    /// <summary>Category of a transport report: delivered (<c>completed</c>) or failed (<c>not-done</c>).</summary>
    public const string DeliveryStatus = "delivery-status";

    /// <summary>Category of a patient acknowledgement.</summary>
    public const string Acknowledgement = "acknowledgement";

    /// <summary>Category of a completed teach-back ("I understand").</summary>
    public const string TeachBack = "teach-back";

    /// <summary>Category of a request to be called.</summary>
    public const string CallMe = "call-me";

    /// <summary>System of the <c>meta.security</c> tag that marks a resource as test data.</summary>
    public const string SimulatedTagSystem = "http://terminology.hl7.org/CodeSystem/v3-ActReason";

    /// <summary>
    /// Code of the <c>meta.security</c> tag (<c>HTEST</c>, "test health data") placed on every
    /// resource the simulator emits, so simulated data is visibly labelled on the wire.
    /// </summary>
    public const string SimulatedTagCode = "HTEST";

    /// <summary>System of the simulator-assigned <c>Communication.identifier</c> used by CareLoop as the source event ID.</summary>
    public const string IdentifierSystem = "https://careloop.invalid/fhir/NamingSystem/patient-channel-simulator";
}

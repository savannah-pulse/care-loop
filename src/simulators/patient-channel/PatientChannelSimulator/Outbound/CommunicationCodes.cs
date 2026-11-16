namespace PatientChannelSimulator.Outbound;

/// <summary>
/// The codes this simulator puts on outbound FHIR <c>Communication</c> resources.
/// </summary>
/// <remarks>
/// <c>Communication.category</c> is how CareLoop tells a transport report from a patient
/// response, and one patient response from another. That distinction is safety-relevant: only an
/// acknowledgement (or teach-back) completes the notification track; a delivery report or a
/// confidence rating never does. The simulator shares no code with CareLoop, so these values are
/// repeated here and must be kept equal to CareLoop's <c>StatusMap</c> and simulator notification
/// provider by agreement between the workstreams.
/// </remarks>
public static class CommunicationCodes
{
    /// <summary>Code system of the category codes below (non-resolvable).</summary>
    public const string CategorySystem = "https://careloop.invalid/fhir/CodeSystem/communication-category";

    /// <summary>Category of a transport report: delivered (<c>completed</c>) or failed (<c>not-done</c>).</summary>
    public const string DeliveryStatus = "delivery-status";

    /// <summary>Category of a patient acknowledgement.</summary>
    public const string Acknowledgement = "acknowledgement";

    /// <summary>Category of a completed teach-back ("I understand").</summary>
    public const string TeachBack = "teach-back";

    /// <summary>Category of a request to be called.</summary>
    public const string CallMe = "call-me";

    /// <summary>Category of a 1 to 5 confidence rating (C10); the value travels in the first <c>payload.contentString</c>.</summary>
    public const string ConfidenceRating = "confidence-rating";

    /// <summary>System of the <c>meta.tag</c> that marks a resource as simulated data.</summary>
    public const string SimulatedTagSystem = "https://careloop.invalid/fhir/CodeSystem/data-origin";

    /// <summary>
    /// Code of the <c>meta.tag</c> placed on every resource the simulator emits, so simulated data
    /// is visibly labelled on the wire. It is a tag, not a security label: nothing should apply
    /// access control because of it.
    /// </summary>
    public const string SimulatedTagCode = "simulated";

    /// <summary>Display text of the simulated-data tag.</summary>
    public const string SimulatedTagDisplay = "SIMULATED - synthetic data";

    /// <summary>
    /// System of the simulator-assigned <c>Communication.identifier</c>. It repeats the logical id
    /// for readers of the log; CareLoop deduplicates on the logical id and <c>meta.versionId</c>, not on this.
    /// </summary>
    public const string IdentifierSystem = "https://careloop.invalid/fhir/NamingSystem/patient-channel-simulator";

    /// <summary>Identifier system of a simulator inbox address, as CareLoop names the recipient of a <c>CommunicationRequest</c>.</summary>
    public const string SimulatorInboxSystem = "https://careloop.invalid/fhir/sid/simulator-inbox";

    /// <summary>Value, in <see cref="IdentifierSystem"/>, that names this simulator as the sender of a transport report.</summary>
    public const string ChannelDeviceIdentifier = "patient-channel-simulator";
}

namespace PatientChannelSimulator.State;

/// <summary>
/// Direction of a logged FHIR exchange, seen from the patient-channel simulator.
/// </summary>
public enum FhirMessageDirection
{
    /// <summary>CareLoop called the simulator (for example a <c>CommunicationRequest</c>).</summary>
    Inbound,

    /// <summary>The simulator called CareLoop (a <c>Communication</c>, or a <c>Task</c> read).</summary>
    Outbound,
}

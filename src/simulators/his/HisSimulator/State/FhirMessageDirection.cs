namespace HisSimulator.State;

/// <summary>
/// Which way a logged FHIR message travelled, seen from the HIS simulator.
/// </summary>
public enum FhirMessageDirection
{
    /// <summary>Sent by the HIS simulator to CareLoop's FHIR endpoints.</summary>
    Sent,

    /// <summary>Received by the HIS simulator from CareLoop.</summary>
    Received,
}

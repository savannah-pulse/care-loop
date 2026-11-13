namespace HisSimulator.Outbound;

/// <summary>
/// Where the simulator finds CareLoop; bound from the <c>CareLoop</c> configuration section.
/// </summary>
public sealed class CareLoopOptions
{
    /// <summary>Name of the configuration section these options bind to.</summary>
    public const string SectionName = "CareLoop";

    /// <summary>
    /// Base URL of CareLoop's FHIR R4 boundary, for example <c>http://localhost:5080/fhir</c>.
    /// </summary>
    /// <remarks>
    /// This is the only CareLoop address the simulator knows. External systems talk to CareLoop
    /// only through <c>/fhir/...</c>; the simulator must never call CareLoop's dashboard REST endpoints.
    /// </remarks>
    public string FhirBaseUrl { get; set; } = "http://localhost:5080/fhir";
}

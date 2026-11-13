namespace PatientChannelSimulator.Outbound;

/// <summary>
/// Where CareLoop can be reached, bound from the <c>CareLoop</c> configuration section.
/// </summary>
/// <remarks>
/// The simulator reaches CareLoop in two ways. As a notification channel it calls CareLoop
/// back on the simulator provider's callback address, the way an SMS or email gateway calls
/// its customer back. To show a patient the status of their case it reads CareLoop's FHIR
/// endpoints. It must never be pointed at a live endpoint.
/// </remarks>
public sealed class CareLoopOptions
{
    /// <summary>Name of the configuration section these options bind to.</summary>
    public const string SectionName = "CareLoop";

    /// <summary>
    /// Base URL of CareLoop's FHIR R4 endpoints, for example <c>http://localhost:5080/fhir</c>.
    /// </summary>
    public string FhirBaseUrl { get; set; } = "http://localhost:5080/fhir";

    /// <summary>
    /// Address CareLoop exposes for the simulator notification provider's callbacks. Delivery
    /// reports and patient responses are posted here, not to the FHIR endpoints.
    /// </summary>
    public string NotificationCallbackUrl { get; set; } = "http://localhost:5080/notifications/inbound/simulator";
}

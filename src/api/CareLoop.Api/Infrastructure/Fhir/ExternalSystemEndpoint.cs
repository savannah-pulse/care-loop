namespace CareLoop.Api.Infrastructure.Fhir;

/// <summary>
/// The address of one external system CareLoop exchanges FHIR with.
/// </summary>
public sealed class ExternalSystemEndpoint
{
    /// <summary>Base URL of the system, without the <c>/fhir</c> suffix.</summary>
    public string? BaseUrl { get; set; }
}

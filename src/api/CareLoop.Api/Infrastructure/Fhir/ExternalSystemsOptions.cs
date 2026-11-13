namespace CareLoop.Api.Infrastructure.Fhir;

/// <summary>
/// The external systems CareLoop exchanges FHIR with, bound to the <c>ExternalSystems</c>
/// configuration section.
/// </summary>
/// <remarks>
/// <para>
/// A deployment can be connected to any number of systems of record: hospital information
/// systems, EMRs, lab systems, or a single health information exchange that fronts them all.
/// Each is one entry in <see cref="Systems"/>, keyed by its system identifier.
/// </para>
/// <para>
/// The key must equal the source system identifier that system uses in the events it sends,
/// because that is what a case records as its source and what deduplication is keyed on. It is
/// also how an outbound message about a case finds its way back to the system the case came from.
/// </para>
/// <para>
/// Channels for reaching patients and staff are not listed here. They are notification
/// providers, each with its own settings under <c>Notifications</c>.
/// </para>
/// <para>
/// CareLoop does not know what is behind an address. In development the entries point at the
/// HIS simulator (see <c>appsettings.Development.json</c>); in production they point at real
/// systems. The base settings contain no systems, so a deployment sends nowhere until it is
/// configured, and there is deliberately no fallback system: a message that cannot be routed
/// must fail, never go to a different hospital.
/// </para>
/// </remarks>
public sealed class ExternalSystemsOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "ExternalSystems";

    /// <summary>The configured systems, keyed by system identifier (case-insensitive).</summary>
    public Dictionary<string, ExternalSystemEndpoint> Systems { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

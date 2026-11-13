namespace CareLoop.Api.Infrastructure.Auth;

/// <summary>
/// The demo identity scheme used by the prototype.
/// </summary>
/// <remarks>
/// The hackathon prototype has no real authentication: the dashboard states who is acting through
/// two request headers, and inbound FHIR names the actor in the resource. This is acceptable only
/// while all data is synthetic: it must be replaced by real authentication and authorization
/// before any deployment handles real patient data. The role keys here must match the role keys used in the pathway
/// definition, which is what the Domain rules check.
/// </remarks>
public static class DemoRoles
{
    /// <summary>Request header carrying the acting person's reference.</summary>
    public const string UserHeader = "X-Demo-User";

    /// <summary>Request header carrying the acting person's role key.</summary>
    public const string RoleHeader = "X-Demo-Role";

    /// <summary>A clinician who reviews results, accepts handoffs and authorizes closure.</summary>
    public const string Clinician = "clinician";

    /// <summary>A follow-up coordinator; second step of the escalation ladder.</summary>
    public const string Coordinator = "coordinator";

    /// <summary>The facility lead, who owns the unassigned queue and is the first step of the escalation ladder.</summary>
    public const string FacilityLead = "facility-lead";

    /// <summary>CareLoop itself, for automated coordination changes such as raising an escalation. Never allowed to close a case.</summary>
    public const string System = "system";

    /// <summary>Every role key a person can hold.</summary>
    public static readonly IReadOnlyList<string> HumanRoles = [Clinician, Coordinator, FacilityLead];
}

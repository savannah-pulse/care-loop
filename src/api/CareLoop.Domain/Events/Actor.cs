namespace CareLoop.Domain.Events;

/// <summary>
/// The person or system that performed an action, as reported by the source.
/// </summary>
/// <remarks>
/// Guards on transitions and closure depend on who acted and in what role, and the audit trail
/// must be able to name them. Identifiers are source-system references, never names.
/// </remarks>
public sealed record Actor
{
    /// <summary>Source-system reference of the person or system, for example a FHIR <c>Practitioner</c> reference.</summary>
    public required string Reference { get; init; }

    /// <summary>Role key the actor acted in, matched against the roles named in the pathway definition.</summary>
    public required string Role { get; init; }

    /// <summary>The facility the actor belongs to, when known.</summary>
    public string? FacilityId { get; init; }
}

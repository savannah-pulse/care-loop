using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using Hl7.Fhir.Model;

namespace CareLoop.Api.Features.Fhir.Outbound;

/// <summary>
/// Building rules shared by the outbound mappers: how owners, actors and the patient are referenced.
/// </summary>
/// <remarks>
/// The mirror of the inbound reading rules, so that what CareLoop sends a system can be read back the same way.
/// </remarks>
internal static class OutboundSupport
{
    /// <summary>
    /// References a case owner: a practitioner, or a queue by identifier, with the role and facility extensions.
    /// </summary>
    /// <param name="owner">The owner.</param>
    /// <returns>The reference.</returns>
    public static ResourceReference Reference(CaseOwner owner)
    {
        var reference = owner.IsQueue
            ? new ResourceReference { Identifier = new Identifier(StatusMap.QueueIdentifierSystem, owner.Reference), Display = owner.Role }
            : new ResourceReference(Typed("Practitioner", owner.Reference));

        return WithRole(reference, owner.Role, owner.FacilityId);
    }

    /// <summary>
    /// References an actor, with the role and, when known, the facility extensions.
    /// </summary>
    /// <param name="actor">The actor.</param>
    /// <returns>The reference.</returns>
    public static ResourceReference Reference(Actor actor) =>
        WithRole(new ResourceReference(Typed("Practitioner", actor.Reference)), actor.Role, actor.FacilityId);

    /// <summary>
    /// Prefixes a bare identifier with its resource type; a reference that already has a type is kept.
    /// </summary>
    /// <param name="type">The resource type to assume.</param>
    /// <param name="reference">A logical identifier or a relative reference.</param>
    /// <returns>A relative reference such as <c>Practitioner/SYN-STAFF-0001</c>.</returns>
    public static string Typed(string type, string reference) =>
        reference.Contains('/', StringComparison.Ordinal) ? reference : $"{type}/{reference}";

    /// <summary>
    /// The logical identifier of the case <c>Task</c>.
    /// </summary>
    /// <param name="caseId">CareLoop's case identifier.</param>
    /// <returns>The identifier, the case identifier itself.</returns>
    public static string CaseTaskId(Guid caseId) => caseId.ToString("D");

    private static ResourceReference WithRole(ResourceReference reference, string role, string? facility)
    {
        reference.SetExtension(StatusMap.ActorRoleExtension, new Code(role));
        if (!string.IsNullOrWhiteSpace(facility))
        {
            reference.SetExtension(StatusMap.ActorFacilityExtension, new FhirString(facility));
        }

        return reference;
    }
}

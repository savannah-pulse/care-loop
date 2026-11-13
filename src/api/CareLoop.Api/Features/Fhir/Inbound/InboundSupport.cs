using System.Security.Cryptography;
using System.Text;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using Hl7.Fhir.Model;

namespace CareLoop.Api.Features.Fhir.Inbound;

/// <summary>
/// Reading rules shared by the inbound mappers: identifiers, references, the screening result a
/// resource belongs to, and the person who acted.
/// </summary>
/// <remarks>
/// Translation only. Nothing here decides whether an event is allowed; that is the engine's job.
/// </remarks>
internal static class InboundSupport
{
    private const string ScreeningResultType = "DiagnosticReport";

    /// <summary>
    /// Builds the source event identifier: the resource identifier plus its version.
    /// </summary>
    /// <param name="resource">The inbound resource.</param>
    /// <returns>
    /// <c>{type}/{id}/_history/{versionId}</c>; without a version, the last-updated time; without
    /// either, a hash of the resource content, so that a resend of the same state is still deduplicated
    /// and a changed state is not.
    /// </returns>
    /// <exception cref="FormatException">The resource has no logical identifier.</exception>
    public static string SourceEventId(Resource resource)
    {
        var id = RequireId(resource);
        var prefix = $"{resource.TypeName}/{id}/_history/";

        if (!string.IsNullOrWhiteSpace(resource.Meta?.VersionId))
        {
            return prefix + resource.Meta.VersionId;
        }

        if (resource.Meta?.LastUpdated is { } lastUpdated)
        {
            return prefix + lastUpdated.UtcDateTime.ToString("yyyyMMdd'T'HHmmssfffffff'Z'", System.Globalization.CultureInfo.InvariantCulture);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(FhirJsonCodec.Serialize(resource)));
        return prefix + "sha256-" + Convert.ToHexStringLower(hash.AsSpan(0, 12));
    }

    /// <summary>
    /// Returns the logical identifier of a resource.
    /// </summary>
    /// <param name="resource">The resource.</param>
    /// <returns>Its identifier.</returns>
    /// <exception cref="FormatException">The resource has none.</exception>
    public static string RequireId(Resource resource) =>
        string.IsNullOrWhiteSpace(resource.Id)
            ? throw new FormatException($"The {resource.TypeName} has no id.")
            : resource.Id;

    /// <summary>
    /// The relative reference to a resource, for example <c>Procedure/SYN-PROC-0001</c>.
    /// </summary>
    /// <param name="resource">The resource.</param>
    /// <returns>The reference.</returns>
    public static string ReferenceTo(Resource resource) => $"{resource.TypeName}/{RequireId(resource)}";

    /// <summary>
    /// Splits a literal reference into its type and logical identifier.
    /// </summary>
    /// <param name="reference">A relative or absolute reference, with or without <c>_history</c>; may be <see langword="null"/>.</param>
    /// <returns>The type and identifier, or <see langword="null"/> when the reference is missing, contained (<c>#id</c>) or not of the form <c>Type/id</c>.</returns>
    public static (string Type, string Id)? Parse(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference) || reference.StartsWith('#'))
        {
            return null;
        }

        var parts = reference.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var historyAt = Array.IndexOf(parts, "_history");
        if (historyAt >= 0)
        {
            parts = parts[..historyAt];
        }

        return parts.Length >= 2 ? (parts[^2], parts[^1]) : null;
    }

    /// <summary>
    /// Finds the screening result a resource belongs to.
    /// </summary>
    /// <param name="resource">The inbound resource; its <see cref="StatusMap.ScreeningResultExtension"/> is read first.</param>
    /// <param name="candidates">The standard reference fields of that resource type that may point at the result.</param>
    /// <returns>The logical identifier of the screening <c>DiagnosticReport</c>.</returns>
    /// <exception cref="FormatException">No field points at a <c>DiagnosticReport</c>.</exception>
    public static string RequireResultId(DomainResource resource, params IEnumerable<ResourceReference?> candidates) =>
        FindResultId(resource, candidates)
        ?? throw new FormatException($"The {resource.TypeName} does not refer to the screening DiagnosticReport it belongs to.");

    /// <summary>
    /// Finds the screening result a resource belongs to, or <see langword="null"/> when it names none.
    /// </summary>
    /// <param name="resource">The inbound resource.</param>
    /// <param name="candidates">The standard reference fields that may point at the result.</param>
    /// <returns>The logical identifier of the screening <c>DiagnosticReport</c>, or <see langword="null"/>.</returns>
    public static string? FindResultId(DomainResource resource, params IEnumerable<ResourceReference?> candidates)
    {
        var fromExtension = resource.GetExtension(StatusMap.ScreeningResultExtension)?.Value as ResourceReference;

        foreach (var candidate in candidates.Prepend(fromExtension))
        {
            if (Parse(candidate?.Reference) is { Type: ScreeningResultType } parsed)
            {
                return parsed.Id;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds a contained resource by a local reference such as <c>#pt</c>.
    /// </summary>
    /// <typeparam name="T">The expected resource type.</typeparam>
    /// <param name="container">The resource that may contain it.</param>
    /// <param name="reference">The reference; only local references are looked up.</param>
    /// <returns>The contained resource, or <see langword="null"/>.</returns>
    public static T? Contained<T>(DomainResource container, ResourceReference? reference)
        where T : Resource
    {
        var local = reference?.Reference;
        if (string.IsNullOrWhiteSpace(local) || !local.StartsWith('#'))
        {
            return null;
        }

        return container.Contained.OfType<T>().FirstOrDefault(r => r.Id == local[1..]);
    }

    /// <summary>
    /// Reads the person or queue a reference names as a CareLoop actor.
    /// </summary>
    /// <param name="reference">The reference, carrying the <see cref="StatusMap.ActorRoleExtension"/>.</param>
    /// <returns>
    /// The actor, or <see langword="null"/> when there is no reference or it carries no role. The
    /// reference is the logical identifier without its type, as staff are known elsewhere in CareLoop.
    /// </returns>
    public static Actor? ReadActor(ResourceReference? reference)
    {
        if (reference is null || ReadRole(reference) is not { } role || ReadReference(reference) is not { } id)
        {
            return null;
        }

        return new Actor { Reference = id, Role = role, FacilityId = ReadFacility(reference) };
    }

    /// <summary>
    /// Reads a proposed owner from a reference.
    /// </summary>
    /// <param name="reference">The reference, carrying the role and facility extensions.</param>
    /// <returns>The owner, or <see langword="null"/> when there is no reference or it carries no role.</returns>
    /// <exception cref="FormatException">The reference names a role but no facility.</exception>
    public static CaseOwner? ReadOwner(ResourceReference? reference)
    {
        if (reference is null || ReadRole(reference) is not { } role || ReadReference(reference) is not { } id)
        {
            return null;
        }

        var facility = ReadFacility(reference)
            ?? throw new FormatException($"The owner '{id}' names no facility ({StatusMap.ActorFacilityExtension}).");

        return new CaseOwner
        {
            Reference = id,
            Role = role,
            FacilityId = facility,
            IsQueue = reference.Identifier?.System == StatusMap.QueueIdentifierSystem,
        };
    }

    /// <summary>
    /// The first of the given instants, or the resource's last-updated time.
    /// </summary>
    /// <param name="resource">The inbound resource.</param>
    /// <param name="instants">The source times, in order of preference.</param>
    /// <returns>When the action happened at the source.</returns>
    /// <exception cref="FormatException">The resource carries no time at all.</exception>
    public static DateTimeOffset OccurredAt(Resource resource, params DateTimeOffset?[] instants) =>
        instants.FirstOrDefault(i => i is not null)
        ?? resource.Meta?.LastUpdated
        ?? throw new FormatException($"The {resource.TypeName} carries no time at which it happened.");

    /// <summary>
    /// Converts a FHIR <c>dateTime</c> to an instant.
    /// </summary>
    /// <param name="value">The value; a date without a time zone is read as UTC.</param>
    /// <returns>The instant, or <see langword="null"/> when there is no value.</returns>
    public static DateTimeOffset? Instant(FhirDateTime? value) =>
        value is not null && value.TryToDateTimeOffset(out var instant) ? instant : null;

    private static string? ReadRole(ResourceReference reference) =>
        (reference.GetExtension(StatusMap.ActorRoleExtension)?.Value as Code)?.Value is { Length: > 0 } role ? role : null;

    private static string? ReadFacility(ResourceReference reference) =>
        (reference.GetExtension(StatusMap.ActorFacilityExtension)?.Value as FhirString)?.Value is { Length: > 0 } facility ? facility : null;

    private static string? ReadReference(ResourceReference reference) =>
        Parse(reference.Reference)?.Id
        ?? (string.IsNullOrWhiteSpace(reference.Identifier?.Value) ? null : reference.Identifier.Value);
}

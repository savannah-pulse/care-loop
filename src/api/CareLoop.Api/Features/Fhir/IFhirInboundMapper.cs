using CareLoop.Contracts.Events;
using Hl7.Fhir.Model;

namespace CareLoop.Api.Features.Fhir;

/// <summary>
/// Translates one kind of inbound FHIR resource into internal events.
/// </summary>
/// <remarks>
/// FHIR carries state; the engine wants events. Each mapper works out which transition a resource
/// represents and produces the matching <see cref="EventEnvelope"/>s. A mapper contains no
/// business logic and never touches a case: it only translates, then the FHIR endpoint hands the
/// envelopes to the event ingestor. The source event identifier must be the resource identifier
/// plus its version (or last-updated time) so a resend of the same state is deduplicated.
/// </remarks>
public interface IFhirInboundMapper
{
    /// <summary>The FHIR resource type this mapper handles, for example <c>DiagnosticReport</c>.</summary>
    string ResourceType { get; }

    /// <summary>
    /// Maps a resource to the events it represents.
    /// </summary>
    /// <param name="resource">The parsed FHIR resource; its type matches <see cref="ResourceType"/>.</param>
    /// <param name="sourceSystem">Identifier of the system that sent the resource.</param>
    /// <param name="ct">Cancels the operation.</param>
    /// <returns>Zero or more envelopes; empty when the resource represents no transition CareLoop tracks.</returns>
    /// <exception cref="FormatException">The resource lacks a field the mapping needs (for example an identifier or a status).</exception>
    System.Threading.Tasks.Task<IReadOnlyList<EventEnvelope>> MapAsync(Resource resource, string sourceSystem, CancellationToken ct);
}

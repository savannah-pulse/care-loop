using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HisSimulator.State;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using FhirTask = Hl7.Fhir.Model.Task;

namespace HisSimulator.Outbound;

/// <summary>Independent implementation of the agreed FHIR wire conventions; references no CareLoop code.</summary>
internal static class FhirSupport
{
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);

    /// <summary>Serializes an independently built R4 resource.</summary>
    /// <param name="resource">The resource to send.</param>
    /// <returns>The FHIR JSON body.</returns>
    internal static string Serialize(Resource resource) => JsonSerializer.Serialize(resource, Json);

    /// <summary>Parses strict R4 JSON and exposes malformed payloads as explicit format failures.</summary>
    /// <param name="json">The received body.</param>
    /// <returns>The resource.</returns>
    /// <exception cref="FormatException">The payload is malformed FHIR.</exception>
    internal static Resource Parse(string json)
    {
        try { return JsonSerializer.Deserialize<Resource>(json, Json) ?? throw new FormatException("The FHIR body is empty."); }
        catch (JsonException ex) { throw new FormatException("The body is not valid FHIR R4 JSON.", ex); }
        catch (DeserializationFailedException ex) { throw new FormatException("The body is not valid FHIR R4 JSON.", ex); }
    }

    /// <summary>Namespace of the agreed placeholder codes and extensions.</summary>
    internal const string Base = "https://careloop.invalid/fhir";

    /// <summary>Derives a valid synthetic logical id from an opaque source key.</summary>
    /// <param name="key">The stable source key.</param>
    /// <returns>A deterministic FHIR logical id.</returns>
    internal static string Id(string key) => "SYN-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..32];

    /// <summary>Creates a deterministic inbox id for repeated delivery of one resource.</summary>
    /// <param name="key">Resource type plus logical id.</param>
    /// <returns>The stable inbox id.</returns>
    internal static Guid InboxId(string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0, 16));

    /// <summary>Stamps source identity, event version, real time and the simulator-owned marker.</summary>
    /// <param name="resource">The new resource.</param>
    /// <param name="request">The source event.</param>
    /// <param name="id">Stable logical id, if supplied.</param>
    /// <returns>The same resource for initialization chaining.</returns>
    /// <exception cref="ArgumentException">The source event key is absent.</exception>
    internal static T Stamp<T>(T resource, OutboundEventRequest request, string? id = null) where T : Resource
    {
        if (string.IsNullOrWhiteSpace(request.SourceEventKey)) throw new ArgumentException("A source event key is required.");
        resource.Id = id ?? Id(request.SourceEventKey + "/" + resource.TypeName);
        resource.Meta = new Meta
        {
            Source = "his-simulator", VersionId = Id(request.SourceEventKey)[4..], LastUpdated = request.OccurredAt,
            Tag = [new Coding(Base + "/CodeSystem/data-origin", "simulated", "SIMULATED - synthetic data")],
        };
        return resource;
    }

    /// <summary>Reads a required source-authored parameter without inventing a value.</summary>
    /// <param name="request">The source event.</param>
    /// <param name="key">Required parameter name.</param>
    /// <returns>The nonblank source value.</returns>
    /// <exception cref="ArgumentException">The value is absent or malformed.</exception>
    internal static string Text(OutboundEventRequest request, string key) =>
        OptionalText(request, key) ?? throw new ArgumentException($"The {key} parameter is required.");

    /// <summary>Reads an optional source-authored string.</summary>
    /// <param name="request">The source event.</param>
    /// <param name="key">Parameter name.</param>
    /// <returns>The source value or null when not provided.</returns>
    /// <exception cref="ArgumentException">A supplied value is not a nonblank string.</exception>
    internal static string? OptionalText(OutboundEventRequest request, string key)
    {
        if (!request.Params.TryGetValue(key, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw new ArgumentException($"The {key} parameter must be a nonblank string.");
        return value.GetString();
    }

    /// <summary>Verifies simulator-owned identities before emitting a staff-driven event.</summary>
    /// <param name="staff">The actor supplied by the scenario or person.</param>
    /// <returns>The validated actor.</returns>
    /// <exception cref="ArgumentException">The actor is absent, nonsynthetic or has an unsupported role.</exception>
    internal static ActingStaff Staff(ActingStaff? staff)
    {
        if (staff is null || !SyntheticId(staff.SyntheticId) || staff.Facility is not null && !SyntheticId(staff.Facility)
            || staff.Role is not ("clinician" or "facility-lead" or "coordinator" or "nurse-navigator" or "receiving-clinician" or "lab"))
            throw new ArgumentException("A synthetic staff identifier, supported human role and synthetic facility are required.");
        return staff;
    }

    /// <summary>Recognizes explicitly synthetic identifiers.</summary>
    /// <param name="value">The proposed identifier.</param>
    /// <returns>True only for a nonempty SYN-prefixed identifier.</returns>
    internal static bool SyntheticId(string? value) => value?.StartsWith("SYN-", StringComparison.Ordinal) == true && value.Length > 4;

    /// <summary>Encodes staff role and facility on the FHIR reference.</summary>
    /// <param name="staff">The source actor.</param>
    /// <returns>A practitioner reference usable by the established inbound mappers.</returns>
    /// <exception cref="ArgumentException">The staff identity is invalid.</exception>
    internal static ResourceReference Actor(ActingStaff staff)
    {
        Staff(staff);
        var reference = new ResourceReference("Practitioner/" + staff.SyntheticId);
        reference.Extension.Add(new Extension(Base + "/StructureDefinition/actor-role", new Code(staff.Role)));
        if (staff.Facility is not null) reference.Extension.Add(new Extension(Base + "/StructureDefinition/actor-facility", new FhirString(staff.Facility)));
        return reference;
    }

    /// <summary>Builds a typed placeholder concept in the agreed namespace.</summary>
    /// <param name="system">Code-system suffix.</param>
    /// <param name="code">Unchanged source code.</param>
    /// <returns>The coded concept.</returns>
    internal static CodeableConcept Concept(string system, string code) => new(Base + "/CodeSystem/" + system, code);

    /// <summary>Adds report correlation to a resource lacking a standard report field.</summary>
    /// <param name="resource">The resource to correlate.</param>
    /// <param name="case">The source case.</param>
    internal static void Result(DomainResource resource, SimulatedCase @case) =>
        resource.Extension.Add(new Extension(Base + "/StructureDefinition/screening-result", new ResourceReference("DiagnosticReport/" + @case.DiagnosticReportId)));

    /// <summary>Builds a stable consultation or referral service request.</summary>
    /// <param name="case">The source case.</param>
    /// <param name="request">The source event.</param>
    /// <param name="kind">Consultation or referral.</param>
    /// <returns>The source request.</returns>
    /// <exception cref="ArgumentException">The staff identity is invalid.</exception>
    internal static ServiceRequest Service(SimulatedCase @case, OutboundEventRequest request, string kind) =>
        Stamp(new ServiceRequest
        {
            Status = RequestStatus.Active, Intent = RequestIntent.Order,
            Category = [Concept("service-request-category", kind)],
            Subject = new ResourceReference("Patient/" + @case.Patient.SyntheticId),
            Requester = Actor(Staff(request.Performer)), AuthoredOnElement = new FhirDateTime(request.OccurredAt),
            ReasonReference = [new ResourceReference("DiagnosticReport/" + @case.DiagnosticReportId)],
        }, request, Id(@case.CaseRef + "/" + kind + "-service-request"));

    /// <summary>Builds the stable Task state for a consultation or referral.</summary>
    /// <param name="case">The source case.</param>
    /// <param name="request">The source event.</param>
    /// <param name="kind">Consultation or referral.</param>
    /// <returns>The new Task state.</returns>
    /// <exception cref="ArgumentException">The status or actor is invalid.</exception>
    internal static FhirTask TrackTask(SimulatedCase @case, OutboundEventRequest request, string kind)
    {
        var status = request.Status switch
        {
            "requested" => FhirTask.TaskStatus.Requested, "accepted" => FhirTask.TaskStatus.Accepted,
            "rejected" => FhirTask.TaskStatus.Rejected, "completed" => FhirTask.TaskStatus.Completed,
            _ => throw new ArgumentException("Status must be requested, accepted, rejected or completed."),
        };
        var actor = Actor(Staff(request.Performer));
        var owner = actor;
        if (request.Params.TryGetValue("proposedOwner", out var proposed))
        {
            ActingStaff? staff;
            try { staff = proposed.Deserialize<ActingStaff>(new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
            catch (JsonException ex) { throw new ArgumentException("The proposed owner is malformed.", ex); }
            owner = Actor(Staff(staff));
        }
        var task = Stamp(new FhirTask
        {
            Status = status, Intent = FhirTask.TaskIntent.Order, Code = Concept("task-code", kind),
            Focus = new ResourceReference("ServiceRequest/" + (@case.ResourceIds.GetValueOrDefault(kind + "-service-request")
                ?? Id(@case.CaseRef + "/" + kind + "-service-request"))),
            BasedOn = [new ResourceReference("DiagnosticReport/" + @case.DiagnosticReportId)],
            For = new ResourceReference("Patient/" + @case.Patient.SyntheticId), Requester = actor, Owner = owner,
            LastModifiedElement = new FhirDateTime(request.OccurredAt),
        }, request, Id(@case.CaseRef + "/" + kind + "-task"));
        if (status == FhirTask.TaskStatus.Rejected)
            task.StatusReason = new CodeableConcept { Text = Text(request, "reason") };
        return task;
    }
}

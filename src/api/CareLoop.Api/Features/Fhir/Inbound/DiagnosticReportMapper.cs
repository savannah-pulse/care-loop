using CareLoop.Api.Infrastructure.Fhir;
using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Events;
using Hl7.Fhir.Model;
using DomainContactPoint = CareLoop.Domain.Recipients.ContactPoint;

namespace CareLoop.Api.Features.Fhir.Inbound;

/// <summary>
/// Maps an inbound <c>DiagnosticReport</c> (with its <c>Observation</c>s) to a screening-result or outcome event.
/// </summary>
/// <remarks>
/// Produces a <see cref="ScreeningResultPayload"/> for a new screening report, which opens a case, and an
/// <see cref="OutcomePayload"/> for a follow-up report that refers to an existing result through the
/// <see cref="StatusMap.ScreeningResultExtension"/> (it becomes the evidence reference). Only identifiers and
/// references are carried over; result values and conclusions stay in the HIS.
/// <para>
/// The patient (C20) is read from a contained <c>Patient</c> (<c>subject</c> = <c>#id</c>) or, when the subject
/// is a literal reference and the source system is configured, read from that system. Only the display name,
/// the MRN, the preferred language and the contact points are taken.
/// </para>
/// </remarks>
public sealed class DiagnosticReportMapper : IFhirInboundMapper
{
    private const string DefaultLanguage = "en";
    private const string MedicalRecordTypeSystem = "http://terminology.hl7.org/CodeSystem/v2-0203";

    private static readonly DiagnosticReport.DiagnosticReportStatus[] Released =
    [
        DiagnosticReport.DiagnosticReportStatus.Final,
        DiagnosticReport.DiagnosticReportStatus.Amended,
        DiagnosticReport.DiagnosticReportStatus.Corrected,
        DiagnosticReport.DiagnosticReportStatus.Appended,
    ];

    private readonly FhirHttpClient _client;

    /// <summary>
    /// Creates the mapper.
    /// </summary>
    /// <param name="client">Reads the referenced <c>Patient</c> when it is not contained.</param>
    public DiagnosticReportMapper(FhirHttpClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public string ResourceType => "DiagnosticReport";

    /// <inheritdoc/>
    public async System.Threading.Tasks.Task<IReadOnlyList<EventEnvelope>> MapAsync(Resource resource, string sourceSystem, CancellationToken ct)
    {
        var report = (DiagnosticReport)resource;

        if (report.Status is not { } status || !Released.Contains(status))
        {
            return [];
        }

        var followsUp = InboundSupport.FindResultId(report);
        var envelope = followsUp is null
            ? await ScreeningResultAsync(report, sourceSystem, ct)
            : FollowUp(report, sourceSystem, followsUp);

        return [envelope];
    }

    private async System.Threading.Tasks.Task<EventEnvelope> ScreeningResultAsync(DiagnosticReport report, string sourceSystem, CancellationToken ct)
    {
        var resultId = InboundSupport.RequireId(report);

        var subject = report.Subject ?? throw new FormatException("The DiagnosticReport has no subject.");
        var (patientId, patient) = await PatientAsync(report, subject, sourceSystem, ct);

        var pathwayId = report.Category.Select(c => StatusMap.CodeIn(c, StatusMap.PathwaySystem)).FirstOrDefault(c => c is not null)
            ?? throw new FormatException($"The DiagnosticReport names no pathway (category in {StatusMap.PathwaySystem}).");

        var facilityId = report.Performer
            .Select(p => InboundSupport.Parse(p.Reference))
            .FirstOrDefault(p => p?.Type == "Organization")?.Id
            ?? throw new FormatException("The DiagnosticReport names no performing Organization (the facility).");

        return new EventEnvelope
        {
            SourceSystem = sourceSystem,
            SourceEventId = InboundSupport.SourceEventId(report),
            Type = CaseEventType.ScreeningResultReceived,
            SourceResultId = resultId,
            OccurredAt = InboundSupport.OccurredAt(report, report.Issued, InboundSupport.Instant(report.Effective as FhirDateTime)),
            Actor = null,
            Payload = new ScreeningResultPayload
            {
                PatientReference = patientId,
                PatientDisplayName = patient is null ? null : DisplayName(patient),
                PatientSecondaryIdentifier = patient is null ? null : Mrn(patient),
                PathwayId = pathwayId,
                FacilityId = facilityId,
                ResultReference = InboundSupport.ReferenceTo(report),
                NamedOwner = report.ResultsInterpreter.Select(InboundSupport.ReadActor).FirstOrDefault(a => a is not null),
                PatientLanguage = patient is null ? DefaultLanguage : Language(patient),
                PatientContactPoints = patient is null ? [] : ContactPoints(patient),
            },
        };
    }

    private static EventEnvelope FollowUp(DiagnosticReport report, string sourceSystem, string resultId)
    {
        var kind = StatusMap.ToEvidenceKind(report.Code)
            ?? throw new FormatException($"The follow-up DiagnosticReport carries no evidence kind (code in {StatusMap.EvidenceKindSystem}).");

        return new EventEnvelope
        {
            SourceSystem = sourceSystem,
            SourceEventId = InboundSupport.SourceEventId(report),
            Type = CaseEventType.OutcomeRecorded,
            SourceResultId = resultId,
            OccurredAt = InboundSupport.OccurredAt(report, report.Issued, InboundSupport.Instant(report.Effective as FhirDateTime)),
            Actor = report.ResultsInterpreter.Concat(report.Performer).Select(InboundSupport.ReadActor).FirstOrDefault(a => a is not null),
            Payload = new OutcomePayload
            {
                EvidenceReference = InboundSupport.ReferenceTo(report),
                EvidenceKind = kind,
            },
        };
    }

    private async System.Threading.Tasks.Task<(string PatientId, Patient? Patient)> PatientAsync(
        DiagnosticReport report, ResourceReference subject, string sourceSystem, CancellationToken ct)
    {
        if (InboundSupport.Contained<Patient>(report, subject) is { } contained)
        {
            return (contained.Id!, contained);
        }

        if (InboundSupport.Parse(subject.Reference) is not { Type: "Patient" } literal)
        {
            throw new FormatException("The DiagnosticReport subject is not a Patient.");
        }

        var resolved = await _client.ReadAsync(sourceSystem, "Patient", literal.Id, ct) as Patient;
        return (literal.Id, resolved);
    }

    private static string? DisplayName(Patient patient)
    {
        var name = patient.Name.FirstOrDefault(n => n.Use == HumanName.NameUse.Official) ?? patient.Name.FirstOrDefault();
        if (name is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(name.Text))
        {
            return name.Text;
        }

        var parts = name.Given.Append(name.Family).Where(p => !string.IsNullOrWhiteSpace(p));
        var joined = string.Join(' ', parts);
        return joined.Length == 0 ? null : joined;
    }

    private static string? Mrn(Patient patient) =>
        patient.Identifier.FirstOrDefault(i => i.System == StatusMap.MrnIdentifierSystem)?.Value
        ?? patient.Identifier.FirstOrDefault(i => i.Type?.Coding.Any(c => c.System == MedicalRecordTypeSystem && c.Code == "MR") == true)?.Value;

    private static string Language(Patient patient)
    {
        var communication = patient.Communication.FirstOrDefault(c => c.Preferred == true) ?? patient.Communication.FirstOrDefault();
        return communication?.Language?.Coding.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.Code))?.Code ?? DefaultLanguage;
    }

    private static List<DomainContactPoint> ContactPoints(Patient patient) =>
        patient.Telecom
            .Where(t => !string.IsNullOrWhiteSpace(t.Value))
            .Select(t => (Channel: StatusMap.ToNotificationChannel(t.System), t.Value))
            .Where(t => t.Channel is not null)
            .Select(t => new DomainContactPoint { Channel = t.Channel!.Value, Address = t.Value! })
            .ToList();
}

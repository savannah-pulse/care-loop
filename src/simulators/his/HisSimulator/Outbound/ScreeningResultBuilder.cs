using HisSimulator.State;
using Hl7.Fhir.Model;

namespace HisSimulator.Outbound;

/// <summary>
/// Builds the FHIR representation of a screening result: a <c>DiagnosticReport</c> with its <c>Observation</c>s.
/// </summary>
/// <remarks>
/// A new report opens a case in CareLoop. The report carries references and placeholder codes only;
/// the result category comes from <c>Params["resultCategory"]</c> and is never invented here. The
/// contained <c>Patient</c> carries one contact point, a text-message or email contact
/// (<see cref="ContactOptions"/>), because those are the only ways CareLoop reaches a patient.
/// </remarks>
public sealed class ScreeningResultBuilder
{
    private readonly ContactOptions _contacts;

    /// <summary>
    /// Creates a builder that gives every patient a text-message contact.
    /// </summary>
    public ScreeningResultBuilder()
        : this(Microsoft.Extensions.Options.Options.Create(new ContactOptions()))
    {
    }

    /// <summary>
    /// Creates the builder.
    /// </summary>
    /// <param name="contacts">Says whether patients are reached by text message or by email.</param>
    public ScreeningResultBuilder(Microsoft.Extensions.Options.IOptions<ContactOptions> contacts)
    {
        ArgumentNullException.ThrowIfNull(contacts);
        _contacts = contacts.Value;
    }

    /// <summary>
    /// Builds the screening <c>DiagnosticReport</c> for a new case.
    /// </summary>
    /// <param name="request">
    /// The event to represent; <see cref="OutboundEventRequest.Patient"/> and the <c>resultCategory</c>
    /// and <c>facility</c> params are required.
    /// </param>
    /// <returns>
    /// A final <c>DiagnosticReport</c> with a deterministic logical id derived from
    /// <see cref="OutboundEventRequest.SourceEventKey"/>, the synthetic patient as subject, the facility
    /// as performer, the placeholder result category as conclusion code, its <c>Observation</c>s as
    /// contained resources referenced from <c>result</c>, and a simulated-data tag in <c>meta.tag</c>.
    /// </returns>
    /// <exception cref="ArgumentException">A required value (patient, result category or facility) is missing.</exception>
    /// <exception cref="InvalidOperationException">The configured patient contact channel is neither text message nor email.</exception>
    public DiagnosticReport Build(OutboundEventRequest request)
    {
        var source = request.Patient;
        var facility = FhirSupport.Text(request, "facility");
        var category = FhirSupport.Text(request, "resultCategory");
        if (source is null || !FhirSupport.SyntheticId(source.SyntheticId)
            || !source.DisplayName.StartsWith("Synthetic", StringComparison.Ordinal)
            || !FhirSupport.SyntheticId(facility) || string.IsNullOrWhiteSpace(request.PathwayId)
            || !System.Text.RegularExpressions.Regex.IsMatch(source.Language, "^[A-Za-z]{2,8}(?:-[A-Za-z0-9]{1,8})*$")
            || source.SecondaryIdentifier is not null && !FhirSupport.SyntheticId(source.SecondaryIdentifier)
            || source.ContactAddress is not null && !FhirSupport.SyntheticId(source.ContactAddress))
            throw new ArgumentException("A synthetic patient, facility, MRN/contact and valid pathway/language are required.");
        var patient = new Patient
        {
            Id = source.SyntheticId, Name = [new HumanName { Text = source.DisplayName }],
            Identifier = [new Identifier(FhirSupport.Base + "/sid/mrn", source.Mrn)],
            Telecom = [new ContactPoint { System = _contacts.PatientSystem(), Value = source.ContactAddress ?? source.SyntheticId }],
            Communication = [new Patient.CommunicationComponent
            {
                Language = new CodeableConcept("urn:ietf:bcp:47", source.Language), Preferred = true,
            }],
        };
        var observation = new Observation
        {
            Id = FhirSupport.Id(request.SourceEventKey + "/observation"), Status = ObservationStatus.Final,
            Code = FhirSupport.Concept("result-category", category),
            Value = FhirSupport.Concept("result-category", category), Subject = new ResourceReference("#" + patient.Id),
        };
        var report = FhirSupport.Stamp(new DiagnosticReport
        {
            Status = DiagnosticReport.DiagnosticReportStatus.Final,
            Category = [FhirSupport.Concept("pathway", request.PathwayId)],
            Code = FhirSupport.Concept("result-category", category), ConclusionCode = [FhirSupport.Concept("result-category", category)],
            Subject = new ResourceReference("#" + patient.Id), Performer = [new ResourceReference("Organization/" + facility)],
            Issued = request.OccurredAt, Contained = [patient, observation], Result = [new ResourceReference("#" + observation.Id)],
        }, request);
        if (request.Performer is not null) report.ResultsInterpreter.Add(FhirSupport.Actor(request.Performer));
        return report;
    }

    /// <summary>
    /// Creates the simulator's own record of the case opened by a screening result.
    /// </summary>
    /// <param name="request">The screening-result event.</param>
    /// <param name="report">The report built for it by <see cref="Build"/>.</param>
    /// <returns>A <see cref="SimulatedCase"/> holding the patient, facility, pathway and report id for later events.</returns>
    /// <exception cref="ArgumentException">Patient, pathway, facility or report id is missing.</exception>
    public SimulatedCase CreateCase(OutboundEventRequest request, DiagnosticReport report)
    {
        return new SimulatedCase(request.CaseRef, request.Patient ?? throw new ArgumentException("Patient is required."),
            FhirSupport.Text(request, "facility"), request.PathwayId ?? throw new ArgumentException("Pathway is required."),
            report.Id ?? throw new ArgumentException("The report must have a logical id."), null, new Dictionary<string, string>(), null);
    }
}

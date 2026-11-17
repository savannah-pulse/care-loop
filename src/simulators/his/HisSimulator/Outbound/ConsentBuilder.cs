using HisSimulator.State;
using Hl7.Fhir.Model;

namespace HisSimulator.Outbound;

/// <summary>Builds source-recorded caregiver consent, without inventing consent or clinical content.</summary>
public sealed class ConsentBuilder
{
    /// <summary>Builds an active grant or inactive withdrawal with report correlation.</summary>
    /// <param name="case">The source case.</param>
    /// <param name="request">The source consent status and optional caregiver contact.</param>
    /// <returns>A FHIR Consent understood by the established inbound mapper.</returns>
    /// <exception cref="ArgumentException">The status, actor or synthetic caregiver address is invalid.</exception>
    public Consent Build(SimulatedCase @case, OutboundEventRequest request)
    {
        var status = request.Status switch
        {
            "active" => Consent.ConsentState.Active, "inactive" => Consent.ConsentState.Inactive,
            "rejected" => Consent.ConsentState.Rejected, _ => throw new ArgumentException("Consent status must be active, inactive or rejected."),
        };
        var consent = FhirSupport.Stamp(new Consent
        {
            Status = status, Scope = new CodeableConcept("http://terminology.hl7.org/CodeSystem/consentscope", "patient-privacy"),
            Category = [new CodeableConcept("http://terminology.hl7.org/CodeSystem/consentcategorycodes", "npp")],
            Patient = new ResourceReference("Patient/" + @case.Patient.SyntheticId),
            Performer = [FhirSupport.Actor(FhirSupport.Staff(request.Performer))], DateTimeElement = new FhirDateTime(request.OccurredAt),
            Provision = new Consent.provisionComponent
            {
                Type = status == Consent.ConsentState.Active ? Consent.ConsentProvisionType.Permit : Consent.ConsentProvisionType.Deny,
            },
        }, request, FhirSupport.Id(@case.CaseRef + "/consent"));
        FhirSupport.Result(consent, @case);
        if (status == Consent.ConsentState.Active)
        {
            var address = FhirSupport.Text(request, "caregiverContact");
            if (!FhirSupport.SyntheticId(address)) throw new ArgumentException("The caregiver contact must be synthetic.");
            var caregiver = new RelatedPerson
            {
                Id = FhirSupport.Id(@case.CaseRef + "/caregiver"), Patient = new ResourceReference("Patient/" + @case.Patient.SyntheticId),
                Telecom = [new ContactPoint { System = ContactPoint.ContactPointSystem.Email, Value = address }],
            };
            consent.Contained.Add(caregiver);
            var provision = consent.Provision ?? throw new InvalidOperationException("The consent provision is missing.");
            provision.Actor.Add(new()
            {
                Role = FhirSupport.Concept("role", "caregiver"), Reference = new ResourceReference("#" + caregiver.Id),
            });
        }
        return consent;
    }
}

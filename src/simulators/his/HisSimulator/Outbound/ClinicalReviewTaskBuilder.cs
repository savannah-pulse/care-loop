using HisSimulator.State;
using FhirTask = Hl7.Fhir.Model.Task;

namespace HisSimulator.Outbound;

/// <summary>
/// Builds the review <c>Task</c> that tells CareLoop a clinician has reviewed a result.
/// </summary>
/// <remarks>
/// Urgency is a clinical decision made by a person in the hospital system. The simulator passes the
/// placeholder urgency code from <c>Params["urgency"]</c> through as <c>Task.priority</c> (or a coded
/// extension if the placeholder does not fit the FHIR priority value set) and never derives one.
/// </remarks>
public sealed class ClinicalReviewTaskBuilder
{
    /// <summary>
    /// Builds a completed review <c>Task</c> for a case.
    /// </summary>
    /// <param name="case">The simulated case under review; supplies the patient and the report to put in <c>Task.focus</c>.</param>
    /// <param name="request">The event to represent; a performer and the <c>urgency</c> param are required.</param>
    /// <returns>
    /// A <c>Task</c> with status completed, a code identifying it as a clinical review, <c>focus</c> on the screening
    /// <c>DiagnosticReport</c>, <c>for</c> the synthetic patient, <c>owner</c> the reviewing clinician, the reviewed
    /// urgency, a deterministic id and version, and a simulated-data tag.
    /// </returns>
    /// <exception cref="ArgumentException">The performer or the urgency is missing.</exception>
    public FhirTask Build(SimulatedCase @case, OutboundEventRequest request)
    {
        var task = FhirSupport.Stamp(new FhirTask
        {
            Status = FhirTask.TaskStatus.Completed, Intent = FhirTask.TaskIntent.Order,
            Code = FhirSupport.Concept("task-code", "clinical-review"),
            Focus = new Hl7.Fhir.Model.ResourceReference("DiagnosticReport/" + @case.DiagnosticReportId),
            For = new Hl7.Fhir.Model.ResourceReference("Patient/" + @case.Patient.SyntheticId),
            Owner = FhirSupport.Actor(FhirSupport.Staff(request.Performer)),
            LastModifiedElement = new Hl7.Fhir.Model.FhirDateTime(request.OccurredAt),
        }, request, FhirSupport.Id(@case.CaseRef + "/review"));
        task.Extension.Add(new Hl7.Fhir.Model.Extension(FhirSupport.Base + "/StructureDefinition/reviewed-urgency",
            new Hl7.Fhir.Model.Code(FhirSupport.Text(request, "urgency"))));
        return task;
    }
}

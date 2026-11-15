using HisSimulator.State;
using Hl7.Fhir.Model;
using FhirTask = Hl7.Fhir.Model.Task;

namespace HisSimulator.Outbound;

/// <summary>
/// Builds the closure decision: CareLoop's case <c>Task</c> set to completed, plus a <c>Provenance</c> naming the authorizer.
/// </summary>
/// <remarks>
/// Closure is where CareLoop stops watching a patient, so it needs an authorized human and evidence
/// or a documented exception. The builder sends exactly what the scenario or the clinician supplied,
/// including a closure with no evidence, so that CareLoop's refusal can be demonstrated. It never
/// fills in missing evidence.
/// </remarks>
public sealed class ClosureBuilder
{
    /// <summary>
    /// Builds the case <c>Task</c> in completed state.
    /// </summary>
    /// <param name="case">The simulated case; supplies CareLoop's case <c>Task</c> id and any outcome resource ids.</param>
    /// <param name="request">
    /// The event to represent; the <c>closureReason</c> param is required. Evidence comes from
    /// <c>Params["evidenceFromStep"]</c> (resolved to the outcome resource sent by that step), an explicit
    /// evidence reference, or a documented exception note; all are optional.
    /// </param>
    /// <returns>
    /// A <c>Task</c> with status completed, the typed closure reason in <c>statusReason</c>, the evidence reference
    /// in <c>output</c> when supplied, a deterministic version and a simulated-data tag.
    /// </returns>
    /// <exception cref="ArgumentException">The closure reason is missing.</exception>
    /// <exception cref="InvalidOperationException">CareLoop's case <c>Task</c> id is not known for the case.</exception>
    public FhirTask BuildTask(SimulatedCase @case, OutboundEventRequest request)
    {
        var taskId = @case.CareLoopTaskId ?? throw new InvalidOperationException("The CareLoop case Task id is not known; receive a correlated FHIR notice first.");
        var task = FhirSupport.Stamp(new FhirTask
        {
            Status = FhirTask.TaskStatus.Completed, Intent = FhirTask.TaskIntent.Order,
            Code = FhirSupport.Concept("task-code", "case"),
            StatusReason = FhirSupport.Concept("status-reason", FhirSupport.Text(request, "closureReason")),
            BasedOn = [new ResourceReference("DiagnosticReport/" + @case.DiagnosticReportId)],
            For = new ResourceReference("Patient/" + @case.Patient.SyntheticId),
            LastModifiedElement = new FhirDateTime(request.OccurredAt),
        }, request, taskId);
        var evidence = FhirSupport.OptionalText(request, "evidenceReference");
        var key = FhirSupport.OptionalText(request, "evidenceFromStep") ?? FhirSupport.OptionalText(request, "evidenceResourceKey");
        if (key is not null)
            evidence = @case.ResourceIds.GetValueOrDefault(key) ?? throw new ArgumentException("The cited evidence resource has not been successfully sent.");
        if (evidence is not null) task.Output.Add(new FhirTask.OutputComponent
        {
            Type = FhirSupport.Concept("closure-output", "evidence"), Value = new ResourceReference(evidence),
        });
        var note = FhirSupport.OptionalText(request, "exceptionNote");
        if (note is not null) task.Output.Add(new FhirTask.OutputComponent
        {
            Type = FhirSupport.Concept("closure-output", "exception-note"), Value = new FhirString(note),
        });
        var date = FhirSupport.OptionalText(request, "nextSurveillanceDate");
        if (date is not null)
        {
            if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out _)) throw new ArgumentException("The surveillance date must be YYYY-MM-DD.");
            task.Output.Add(new FhirTask.OutputComponent { Type = FhirSupport.Concept("closure-output", "next-surveillance-date"), Value = new Date(date) });
        }
        if (request.Params.TryGetValue("documentedAttemptReferences", out var attempts))
        {
            if (attempts.ValueKind != System.Text.Json.JsonValueKind.Array) throw new ArgumentException("Documented attempts must be an array of references.");
            foreach (var attempt in attempts.EnumerateArray())
            {
                if (attempt.ValueKind != System.Text.Json.JsonValueKind.String || string.IsNullOrWhiteSpace(attempt.GetString()))
                    throw new ArgumentException("Each documented attempt must be a nonblank reference.");
                task.Output.Add(new FhirTask.OutputComponent { Type = FhirSupport.Concept("closure-output", "documented-attempt"), Value = new ResourceReference(attempt.GetString()) });
            }
        }
        return task;
    }

    /// <summary>
    /// Builds the <c>Provenance</c> that names who authorized the closure.
    /// </summary>
    /// <param name="case">The simulated case being closed.</param>
    /// <param name="request">The event to represent; the performer is the authorizer and is required.</param>
    /// <param name="closureTask">The completed case <c>Task</c> built by <see cref="BuildTask"/>, used as the provenance target.</param>
    /// <returns>
    /// A <c>Provenance</c> targeting the case <c>Task</c>, recorded at <see cref="OutboundEventRequest.OccurredAt"/>,
    /// with one agent (the synthetic authorizer and role), a deterministic id and a simulated-data tag.
    /// </returns>
    /// <exception cref="ArgumentException">The performer is missing.</exception>
    public Provenance BuildProvenance(SimulatedCase @case, OutboundEventRequest request, FhirTask closureTask)
    {
        return FhirSupport.Stamp(new Provenance
        {
            Target = [new ResourceReference("Task/" + closureTask.Id)], Recorded = request.OccurredAt,
            Agent = [new Provenance.AgentComponent { Who = FhirSupport.Actor(FhirSupport.Staff(request.Performer)) }],
        }, request);
    }
}

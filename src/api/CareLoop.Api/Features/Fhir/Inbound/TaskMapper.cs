using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Events;
using CareLoop.Domain.Tracks;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using FhirTask = Hl7.Fhir.Model.Task;
using FhirTaskStatus = Hl7.Fhir.Model.Task.TaskStatus;

namespace CareLoop.Api.Features.Fhir.Inbound;

/// <summary>
/// Maps an inbound <c>Task</c> to a review, consultation, referral or closure event.
/// </summary>
/// <remarks>
/// <c>Task.code</c> in <see cref="StatusMap.TaskCodeSystem"/> says what the task is about: a completed review task
/// becomes a clinical review with the reviewed urgency; a consultation or referral task becomes a status update
/// using <see cref="StatusMap"/> (requested, accepted, rejected, completed); the case task set to completed becomes a
/// closure decision, whose authorizer is the agent of the <c>Provenance</c> in <c>Task.relevantHistory</c> (contained).
/// That <c>Provenance</c> counts only when it targets this task (<c>#</c>, or <c>Task/{id}</c> with a matching version
/// when one is given): otherwise a closure could borrow an authorizer recorded for another resource. Without a
/// matching <c>Provenance</c> the closure names no actor, so the closure gate refuses it and the refusal is audited.
/// A closure that arrives as a <c>Provenance</c> with the case task contained is mapped by <see cref="ProvenanceMapper"/>
/// through <see cref="MapClosure"/>, so both carriers produce the same event.
/// </remarks>
public sealed class TaskMapper : IFhirInboundMapper
{
    /// <inheritdoc/>
    public string ResourceType => "Task";

    /// <inheritdoc/>
    public System.Threading.Tasks.Task<IReadOnlyList<EventEnvelope>> MapAsync(Resource resource, string sourceSystem, CancellationToken ct)
    {
        var task = (FhirTask)resource;

        var envelope = StatusMap.CodeIn(task.Code, StatusMap.TaskCodeSystem) switch
        {
            StatusMap.ClinicalReviewTaskCode => Review(task, sourceSystem),
            StatusMap.ConsultationTaskCode => Consultation(task, sourceSystem),
            StatusMap.ReferralTaskCode => Referral(task, sourceSystem),
            StatusMap.CaseTaskCode => MapClosure(task, task, Authorizer(task), sourceSystem),
            _ => null,
        };

        IReadOnlyList<EventEnvelope> result = envelope is null ? [] : [envelope];
        return System.Threading.Tasks.Task.FromResult(result);
    }

    /// <summary>
    /// Maps a completed case <c>Task</c> to a closure decision.
    /// </summary>
    /// <param name="caseTask">The case task, which carries the reason and the evidence.</param>
    /// <param name="carrier">The resource that arrived: the task itself, or the <c>Provenance</c> containing it; its identifier and version make the source event identifier.</param>
    /// <param name="authorizer">The <c>Provenance</c> naming the authorizer, or <see langword="null"/> when there is none.</param>
    /// <param name="sourceSystem">Identifier of the sending system.</param>
    /// <returns>The closure envelope, or <see langword="null"/> when the task is not completed.</returns>
    /// <exception cref="FormatException">The task names no known closure reason or no screening result.</exception>
    internal static EventEnvelope? MapClosure(FhirTask caseTask, Resource carrier, Provenance? authorizer, string sourceSystem)
    {
        if (caseTask.Status != FhirTaskStatus.Completed)
        {
            return null;
        }

        var reason = StatusMap.ToClosureReason(caseTask.StatusReason)
            ?? throw new FormatException($"The completed case Task names no closure reason (statusReason in {StatusMap.StatusReasonSystem}).");

        var provenanceReference = authorizer is null
            ? caseTask.RelevantHistory.Select(r => r.Reference).FirstOrDefault(r => !string.IsNullOrWhiteSpace(r))
            : $"Provenance/{InboundSupport.RequireId(authorizer)}";

        return new EventEnvelope
        {
            SourceSystem = sourceSystem,
            SourceEventId = InboundSupport.SourceEventId(carrier),
            Type = CaseEventType.ClosureDecided,
            SourceResultId = ResultOf(caseTask),
            OccurredAt = InboundSupport.OccurredAt(carrier, authorizer?.Recorded, InboundSupport.Instant(caseTask.LastModifiedElement)),
            Actor = authorizer?.Agent.Select(a => InboundSupport.ReadActor(a.Who)).FirstOrDefault(a => a is not null),
            Payload = new ClosurePayload
            {
                Reason = reason,
                ProvenanceReference = provenanceReference ?? string.Empty,
                EvidenceReference = Outputs(caseTask, StatusMap.EvidenceOutput).Select(o => (o as ResourceReference)?.Reference).FirstOrDefault(r => r is not null),
                ExceptionNote = Outputs(caseTask, StatusMap.ExceptionNoteOutput).Select(o => (o as FhirString)?.Value).FirstOrDefault(n => n is not null),
                NextSurveillanceDate = Outputs(caseTask, StatusMap.NextSurveillanceDateOutput).Select(o => ToDate(o as Date)).FirstOrDefault(d => d is not null),
                DocumentedAttemptReferences = Outputs(caseTask, StatusMap.DocumentedAttemptOutput)
                    .Select(o => (o as ResourceReference)?.Reference)
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .Select(r => r!)
                    .ToList(),
            },
        };
    }

    private static EventEnvelope? Review(FhirTask task, string sourceSystem)
    {
        if (task.Status != FhirTaskStatus.Completed)
        {
            return null;
        }

        var urgency = (task.GetExtension(StatusMap.ReviewedUrgencyExtension)?.Value as Code)?.Value
            ?? (task.Priority is { } priority ? priority.GetLiteral() : null)
            ?? throw new FormatException("The completed review Task carries no urgency (priority or the reviewed-urgency extension).");

        return new EventEnvelope
        {
            SourceSystem = sourceSystem,
            SourceEventId = InboundSupport.SourceEventId(task),
            Type = CaseEventType.ClinicalReviewCompleted,
            SourceResultId = ResultOf(task),
            OccurredAt = InboundSupport.OccurredAt(task, InboundSupport.Instant(task.LastModifiedElement)),
            Actor = InboundSupport.ReadActor(task.Owner),
            Payload = new ClinicalReviewPayload
            {
                UrgencyCode = urgency,
                ReviewTaskReference = InboundSupport.ReferenceTo(task),
            },
        };
    }

    private static EventEnvelope? Consultation(FhirTask task, string sourceSystem)
    {
        if (StatusMap.ToConsultationStatus(task.Status) is not { } status)
        {
            return null;
        }

        // A requested consultation task that names an owner proposes that person as the new owner (a handoff).
        var proposedOwner = status == ConsultationStatus.Requested ? InboundSupport.ReadOwner(task.Owner) : null;
        if (proposedOwner is not null)
        {
            status = ConsultationStatus.OwnerProposed;
        }

        return new EventEnvelope
        {
            SourceSystem = sourceSystem,
            SourceEventId = InboundSupport.SourceEventId(task),
            Type = CaseEventType.ConsultationUpdated,
            SourceResultId = ResultOf(task),
            OccurredAt = InboundSupport.OccurredAt(task, InboundSupport.Instant(task.LastModifiedElement)),
            Actor = proposedOwner is null ? InboundSupport.ReadActor(task.Owner) : InboundSupport.ReadActor(task.Requester),
            Payload = new ConsultationUpdatePayload
            {
                Status = status,
                ServiceRequestReference = ServiceRequestOf(task),
                TaskReference = InboundSupport.ReferenceTo(task),
                ProposedOwner = proposedOwner,
                Reason = status == ConsultationStatus.Rejected ? ReasonText(task) : null,
            },
        };
    }

    private static EventEnvelope? Referral(FhirTask task, string sourceSystem)
    {
        if (StatusMap.ToReferralStatus(task.Status) is not { } status)
        {
            return null;
        }

        return new EventEnvelope
        {
            SourceSystem = sourceSystem,
            SourceEventId = InboundSupport.SourceEventId(task),
            Type = CaseEventType.ReferralUpdated,
            SourceResultId = ResultOf(task),
            OccurredAt = InboundSupport.OccurredAt(task, InboundSupport.Instant(task.LastModifiedElement)),
            Actor = InboundSupport.ReadActor(task.Owner),
            Payload = new ReferralUpdatePayload
            {
                Status = status,
                ServiceRequestReference = ServiceRequestOf(task),
                TaskReference = InboundSupport.ReferenceTo(task),
                ReceivingFacilityId = InboundSupport.Parse(task.Owner?.Reference) is { Type: "Organization" } org ? org.Id : null,
                Reason = status == ReferralStatus.Rejected ? ReasonText(task) : null,
            },
        };
    }

    private static Provenance? Authorizer(FhirTask task) =>
        task.RelevantHistory
            .Select(r => InboundSupport.Contained<Provenance>(task, r))
            .FirstOrDefault(p => p is not null && p.Target.Any(t => TargetsTask(t.Reference, task)));

    private static bool TargetsTask(string? reference, FhirTask task)
    {
        // A contained resource names its container as "#".
        if (reference == "#")
        {
            return true;
        }

        if (InboundSupport.Parse(reference) is not { Type: "Task" } target || !string.Equals(target.Id, task.Id, StringComparison.Ordinal))
        {
            return false;
        }

        var history = reference!.IndexOf("/_history/", StringComparison.Ordinal);
        return history < 0 || string.Equals(reference[(history + "/_history/".Length)..], task.Meta?.VersionId, StringComparison.Ordinal);
    }

    private static string ResultOf(FhirTask task) =>
        InboundSupport.RequireResultId(task, task.BasedOn.Prepend(task.Focus));

    private static string ServiceRequestOf(FhirTask task) =>
        task.BasedOn.Prepend(task.Focus)
            .Where(r => InboundSupport.Parse(r?.Reference) is { Type: "ServiceRequest" })
            .Select(r => r!.Reference)
            .FirstOrDefault()
        ?? throw new FormatException("The Task does not refer to its ServiceRequest (focus or basedOn).");

    private static DateOnly? ToDate(Date? date) =>
        DateOnly.TryParseExact(date?.Value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var value)
            ? value
            : null;

    private static string? ReasonText(FhirTask task) =>
        string.IsNullOrWhiteSpace(task.StatusReason?.Text) ? null : task.StatusReason.Text;

    private static IEnumerable<DataType> Outputs(FhirTask task, string type) =>
        task.Output
            .Where(o => StatusMap.CodeIn(o.Type, StatusMap.ClosureOutputSystem) == type && o.Value is not null)
            .Select(o => o.Value);
}

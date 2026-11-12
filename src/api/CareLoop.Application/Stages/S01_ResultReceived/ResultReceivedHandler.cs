using System.Text.Json;
using CareLoop.Application.Intake;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;

namespace CareLoop.Application.Stages.S01_ResultReceived;

/// <summary>
/// Stage 1 entry. Opens one coordination case per (source system, result reference); the automatic steps for
/// stages 2 and 3 then run in the same transaction. A second event for the same result is held for review.
/// </summary>
public sealed class ResultReceivedHandler : ICaseEventHandler
{
    public string EventType => CaseEventTypes.ResultReceived;

    public ActorPermission Permission => ActorPermission.SourceSystems;

    public async Task<HandlerResult> HandleAsync(CaseEventContext context, CancellationToken cancellationToken)
    {
        var envelope = context.Envelope;
        var errors = new Dictionary<string, string[]>();
        if (envelope.PatientRef is null)
        {
            errors["patientRef"] = ["patientRef (synthetic reference) is required."];
        }

        if (envelope.ResultRef is null)
        {
            errors["resultRef"] = ["resultRef (synthetic reference) is required."];
        }

        if (errors.Count > 0)
        {
            return HandlerResult.Invalid(errors);
        }

        var existing = await context.Cases.FindByResultAsync(envelope.SourceSystem, envelope.ResultRef!, cancellationToken);
        if (existing is not null)
        {
            // A new source event for a known result may be a correction or a duplicate: never apply it silently.
            var held = context.AppendPrimaryEvent(existing, EventDisposition.HeldForReview);
            existing.RaiseException(
                ExceptionType.ConflictingSourceEvent,
                $"A different source event ({envelope.SourceEventId}) reported a result that already has a case.",
                held.Id, context.Now);
            return HandlerResult.HeldForReview(
                existing, "result_already_has_case",
                "This result already has a coordination case; the new event was recorded and held for human review.");
        }

        var @case = context.OpenCase(envelope.PatientRef!, envelope.ResultRef!);

        // Urgency is supplied by a reviewing human at the source; absence becomes review work, never a guess.
        if (!HasReviewedUrgency(envelope.Payload))
        {
            @case.RaiseException(
                ExceptionType.ReviewedUrgencyMissing,
                "The source did not supply a reviewed urgency. Urgency must come from an authorized reviewer.",
                context.PrimaryEventId, context.Now);
        }

        return HandlerResult.Accepted(@case);
    }

    private static bool HasReviewedUrgency(JsonElement? payload) =>
        payload is { ValueKind: JsonValueKind.Object } p
        && p.TryGetProperty("reviewedUrgency", out var urgency)
        && urgency.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(urgency.GetString());
}

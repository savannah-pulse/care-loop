using CareLoop.Application.Abstractions;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using Microsoft.Extensions.Logging;

namespace CareLoop.Application.Intake;

public interface IEventIntakeService
{
    Task<IntakeResult> IngestAsync(EventEnvelope envelope, CancellationToken cancellationToken);
}

/// <summary>
/// The single entry point for events. Order: validate, route, authorize, deduplicate, handle, advance
/// automatic stages, then commit once. Nothing is persisted unless the whole sequence succeeds.
/// </summary>
public sealed class EventIntakeService(
    IEnumerable<ICaseEventHandler> handlers,
    AutomaticStageRunner stageRunner,
    ICaseRepository cases,
    ICaseEventStore events,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<EventIntakeService> logger) : IEventIntakeService
{
    public async Task<IntakeResult> IngestAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        var result = await ProcessAsync(envelope, cancellationToken);
        logger.LogInformation(
            "Intake {EventType} from {SourceSystem} -> {Status} {Code}",
            envelope.EventType, envelope.SourceSystem, result.Status, result.Code);
        return result;
    }

    private async Task<IntakeResult> ProcessAsync(EventEnvelope envelope, CancellationToken ct)
    {
        var (validated, errors) = EnvelopeValidator.Validate(envelope);
        if (validated is null)
        {
            return IntakeResult.Invalid(errors);
        }

        var handler = handlers.FirstOrDefault(h => h.EventType == validated.EventType);
        if (handler is null)
        {
            return new IntakeResult(
                IntakeStatus.UnsupportedEventType, Code: "unsupported_event_type",
                Message: $"No handler is registered for event type '{validated.EventType}'.");
        }

        if (!handler.Permission.Allows(validated.Actor))
        {
            return new IntakeResult(
                IntakeStatus.Forbidden, Code: "actor_not_permitted",
                Message: $"The actor is not permitted to submit '{validated.EventType}'.");
        }

        var existing = await events.FindBySourceKeyAsync(validated.SourceSystem, validated.SourceEventId, ct);
        if (existing is not null)
        {
            return await ResolveReplayAsync(existing, validated, ct);
        }

        var now = clock.GetUtcNow();
        var resolvedCase = validated.CaseId is { } caseId ? await cases.GetAsync(caseId, ct) : null;
        var context = new CaseEventContext(validated, now, cases, events, resolvedCase);

        var outcome = await handler.HandleAsync(context, ct);
        switch (outcome.Outcome)
        {
            case HandlerOutcome.Invalid:
                return new IntakeResult(IntakeStatus.Invalid, Code: outcome.Code, Message: outcome.Message, Errors: outcome.Errors);
            case HandlerOutcome.NotFound:
                return new IntakeResult(IntakeStatus.NotFound, Code: outcome.Code, Message: outcome.Message);
            case HandlerOutcome.Rejected:
                return new IntakeResult(IntakeStatus.Rejected, Code: outcome.Code, Message: outcome.Message);
        }

        var @case = outcome.Case ?? throw new InvalidOperationException(
            $"Handler for '{validated.EventType}' returned {outcome.Outcome} without a case.");
        var primary = context.PrimaryEvent ?? throw new InvalidOperationException(
            $"Handler for '{validated.EventType}' did not append the primary event.");

        if (outcome.Outcome == HandlerOutcome.Accepted)
        {
            await stageRunner.RunAsync(context, @case, ct);
        }

        var saveFailure = await TrySaveAsync(ct);
        if (saveFailure is not null)
        {
            return saveFailure;
        }

        return outcome.Outcome == HandlerOutcome.HeldForReview
            ? new IntakeResult(IntakeStatus.HeldForReview, primary.Id, @case.Id, @case.Stage, outcome.Code, outcome.Message)
            : IntakeResult.Accepted(primary.Id, @case);
    }

    // The same source event ID returns its original outcome; reusing it with different content needs human review.
    private async Task<IntakeResult> ResolveReplayAsync(CaseEvent existing, ValidatedEnvelope envelope, CancellationToken ct)
    {
        var @case = await cases.GetAsync(existing.CaseId, ct);

        if (existing.ContentHash == envelope.ContentHash)
        {
            return existing.Disposition == EventDisposition.HeldForReview
                ? new IntakeResult(
                    IntakeStatus.HeldForReview, existing.Id, existing.CaseId, @case?.Stage,
                    "source_event_held_for_review", "This event was previously held for human review.")
                : IntakeResult.Duplicate(existing.Id, existing.CaseId, @case?.Stage);
        }

        if (@case is null)
        {
            return new IntakeResult(IntakeStatus.Rejected, Code: "source_event_content_conflict",
                Message: "The source event ID was already used with different content.");
        }

        @case.RaiseException(
            ExceptionType.ConflictingSourceEvent,
            $"Source event {envelope.SourceSystem}/{envelope.SourceEventId} was re-sent with different content.",
            existing.Id, clock.GetUtcNow());

        var saveFailure = await TrySaveAsync(ct);
        return saveFailure ?? new IntakeResult(
            IntakeStatus.HeldForReview, existing.Id, @case.Id, @case.Stage,
            "source_event_content_conflict",
            "The source event ID was already used with different content; review work was raised.");
    }

    private async Task<IntakeResult?> TrySaveAsync(CancellationToken ct)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
            return null;
        }
        catch (Exception ex) when (ex is DuplicateKeyException or ConcurrencyConflictException)
        {
            // Idempotent delivery makes a resend safe, and it will then see the winner's result.
            return new IntakeResult(
                IntakeStatus.ConcurrentUpdate, Code: "concurrent_update",
                Message: "Another request changed the same case or event. Resend the same event.");
        }
    }
}

using CareLoop.Api.Features.Common;
using CareLoop.Api.Features.Events;
using CareLoop.Api.Infrastructure.Auth;
using CareLoop.Contracts.Cases;
using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Time;
using CareLoop.Domain.Tracks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CareLoop.Api.Features.Cases;

/// <summary>
/// Records what a nurse learned from the patient on a call: that she will come, that she said the
/// next step back, and how confident she feels.
/// </summary>
/// <remarks>
/// <para>
/// CareLoop reaches patients by text message and email only. A reply on those channels can say "I will
/// come" or ask for a call, and nothing more, so teach-back and the confidence rating are recorded here,
/// by a person, after a call. Only a nurse navigator may record them.
/// </para>
/// <para>
/// Each outcome becomes one <see cref="CaseEventType.NotificationUpdated"/> event with the nurse as its
/// actor and no channel, and goes through <see cref="EventIngestor"/> like every other event, so the same
/// rules apply and both an applied and a refused outcome are audited. Recording an outcome never changes
/// who is responsible for the case.
/// </para>
/// </remarks>
public static class RecordCallOutcome
{
    /// <summary>Maps the call-outcome route.</summary>
    /// <param name="routes">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapRecordCallOutcome(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/cases/{caseId:guid}/call-outcome", Handle)
            .WithName("RecordCallOutcome").WithTags("Cases").Produces<CaseDetailResponse>().Produces(404)
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(409);
        return routes;
    }

    /// <summary>Records the outcome of a call through the one intake pipeline.</summary>
    /// <param name="caseId">The case.</param>
    /// <param name="reason">Access reason required outside the nurse's facility.</param>
    /// <param name="request">What the patient said on the call.</param>
    /// <param name="context">The staff identity.</param>
    /// <param name="repository">Reads the case to check access and to answer with its new state.</param>
    /// <param name="ingestor">The single intake pipeline the outcomes are recorded through.</param>
    /// <param name="pathways">The display-label rules.</param>
    /// <param name="clock">The time the outcome is recorded at.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>
    /// The updated case; 400 when nothing was recorded or the rating is not from 1 to 5; 403 when the
    /// person is not a nurse navigator or may not open the case; 404 for an unknown case; 409 when a
    /// rule refused an outcome (for example no notification was ever requested for the case).
    /// </returns>
    public static async Task<IResult> Handle(Guid caseId, [FromQuery] string? reason, [FromBody] CallOutcomeRequest request,
        HttpContext context, ICaseRepository repository, EventIngestor ingestor, IPathwayProvider pathways, IClock clock, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (ReadAccess.Identity(context, out var actor) is { } identityError) return identityError;
        if (ReadAccess.ValidateReason(reason) is { } invalid) return invalid;
        if (!string.Equals(actor.Role, DemoRoles.NurseNavigator, StringComparison.OrdinalIgnoreCase))
            return Results.Problem("Only a nurse navigator may record the outcome of a call with a patient.", statusCode: 403);
        if (!request.Acknowledged && !request.Understood && request.Confidence is null)
            return Results.Problem("Record at least one outcome: that the patient will come, that she said the next step back, or her confidence rating.", statusCode: 400);
        if (request.Confidence is < NotificationTrack.MinimumConfidence or > NotificationTrack.MaximumConfidence)
            return Results.Problem($"A confidence rating must be between {NotificationTrack.MinimumConfidence} and {NotificationTrack.MaximumConfidence}.", statusCode: 400);

        var @case = await repository.GetAsync(caseId, ct);
        if (@case is null) return Results.NotFound();
        if (ReadAccess.CasePermission(@case, actor, reason) is { } refusal) return refusal;

        // The same call, so the same time and the same reference; one event per outcome keeps them apart.
        var now = clock.UtcNow;
        var call = Guid.NewGuid();
        var outcomes = new List<(NotificationUpdateKind Kind, int? Confidence)>();
        if (request.Acknowledged) outcomes.Add((NotificationUpdateKind.Acknowledged, null));
        if (request.Understood) outcomes.Add((NotificationUpdateKind.Understood, null));
        if (request.Confidence is { } rating) outcomes.Add((NotificationUpdateKind.ConfidenceRated, rating));

        // Each outcome is its own event, so one can be refused after another was applied; the answer then says
        // which were recorded, so the nurse does not take the whole call as lost. Sending them again is harmless.
        var recorded = new List<NotificationUpdateKind>();
        try
        {
            foreach (var (kind, confidence) in outcomes)
            {
                var result = await ingestor.IngestAsync(new EventEnvelope
                {
                    SourceSystem = @case.SourceSystem,
                    SourceEventId = $"call-outcome/{call:N}/{kind}",
                    Type = CaseEventType.NotificationUpdated,
                    SourceResultId = @case.SourceResultId,
                    OccurredAt = now,
                    // A copy each time: the stored event owns its actor, so one instance cannot serve two events.
                    Actor = actor with { },
                    Payload = new NotificationUpdatePayload
                    {
                        Kind = kind,
                        Channel = null,
                        CommunicationReference = $"call-outcome/{call:D}",
                        Confidence = confidence,
                    },
                }, ct);
                if (result.Outcome != IngestOutcome.Applied)
                    return Results.Problem(PartlyRecorded(result.Reason ?? "The outcome could not be recorded.", recorded), statusCode: 409);
                recorded.Add(kind);
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Problem(PartlyRecorded("The case changed while recording the call. Refresh and try again.", recorded), statusCode: 409);
        }

        var updated = await repository.GetAsync(caseId, ct);
        return updated is null ? Results.NotFound() : Results.Ok(CaseReadMapping.Detail(updated, actor, pathways));
    }

    private static string PartlyRecorded(string reason, IReadOnlyList<NotificationUpdateKind> recorded) =>
        recorded.Count == 0
            ? reason
            : $"{reason} Already recorded from this call: {string.Join(", ", recorded)}.";
}

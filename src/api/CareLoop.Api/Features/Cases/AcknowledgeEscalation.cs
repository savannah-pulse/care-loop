using System.Text.Json;
using CareLoop.Api.Features.Common;
using CareLoop.Api.Infrastructure.Auth;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Contracts.Cases;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Time;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CareLoop.Api.Features.Cases;

/// <summary>Records a human seeing a specific escalation, never completion of the overdue work.</summary>
public static class AcknowledgeEscalation
{
    /// <summary>Maps the human escalation-acknowledgement route.</summary>
    /// <param name="routes">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapAcknowledgeEscalation(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/cases/{caseId:guid}/escalations/{escalationId:guid}/acknowledgement", Handle)
            .WithName("AcknowledgeEscalation").WithTags("Cases").Produces<CaseDetailResponse>().Produces(404)
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(409);
        return routes;
    }

    /// <summary>Saves the acknowledgement through the transactional case write path.</summary>
    /// <param name="caseId">The case.</param>
    /// <param name="escalationId">The exact escalation the person saw.</param>
    /// <param name="reason">Access reason required outside the reader's facility.</param>
    /// <param name="context">The staff identity.</param>
    /// <param name="repository">The single case write path.</param>
    /// <param name="db">Reads the source-recorded patient language for the response.</param>
    /// <param name="pathways">The display-label rules.</param>
    /// <param name="clock">The acknowledgement clock.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Updated state, 404, or a 403/409 refusal that leaves accountability and deadlines untouched.</returns>
    public static async Task<IResult> Handle(Guid caseId, Guid escalationId, [FromQuery] string? reason,
        HttpContext context, ICaseRepository repository, CareLoopDbContext db, IPathwayProvider pathways, IClock clock, CancellationToken ct)
    {
        if (ReadAccess.Identity(context, out var actor) is { } identityError) return identityError;
        if (ReadAccess.ValidateReason(reason) is { } invalid) return invalid;
        var @case = await repository.GetAsync(caseId, ct);
        if (@case is null) return Results.NotFound();
        var refusal = ReadAccess.CasePermission(@case, actor, reason);
        var target = @case.Escalations.FirstOrDefault(e => e.Id == escalationId);
        var status = 409;
        string? rejection = null;
        if (refusal is not null)
        {
            status = 403;
            rejection = "Escalation access is not permitted for this reader.";
        }
        else if (target is not null && !string.Equals(target.EscalatedTo.Role, actor.Role, StringComparison.OrdinalIgnoreCase))
        {
            status = 403;
            rejection = "Only the role addressed by this escalation may acknowledge it.";
        }
        else if (target is not null && actor.Role != DemoRoles.Coordinator && target.EscalatedTo.FacilityId is { } addressed
            && !string.Equals(addressed, actor.FacilityId, StringComparison.Ordinal))
        {
            status = 403;
            rejection = "Only the addressed facility's role may acknowledge this escalation.";
        }
        else
        {
            try { @case.AcknowledgeEscalation(escalationId, actor, clock.UtcNow); }
            catch (InvalidOperationException ex) { rejection = ex.Message; }
        }
        var now = clock.UtcNow;
        try
        {
            await repository.SaveAsync(@case, new SourceEvent
            {
                Id = Guid.NewGuid(), SourceSystem = EventOrigins.CareLoop, Origin = EventOrigins.CareLoop, SourceEventId = Guid.NewGuid().ToString("N"),
                Type = CaseEventType.EscalationAcknowledged, CaseId = caseId, Actor = actor,
                OccurredAt = now, ReceivedAt = now, PayloadJson = JsonSerializer.Serialize(new { escalationId, reason }),
                Disposition = rejection is null ? SourceEventDisposition.Applied : SourceEventDisposition.Rejected,
                DispositionReason = rejection ?? "Escalation acknowledged; deadline and owner unchanged",
            }, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Problem("The case changed while acknowledging. Refresh and try again.", statusCode: 409);
        }
        return rejection is null ? Results.Ok(await CaseReadMapping.DetailAsync(@case, actor, pathways, db, ct))
            : Results.Problem(rejection, statusCode: status);
    }
}

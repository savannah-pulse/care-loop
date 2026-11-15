using CareLoop.Api.Features.Common;
using CareLoop.Api.Infrastructure.Auth;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Contracts.Cases;
using CareLoop.Domain.Time;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CareLoop.Api.Features.Cases;

/// <summary>Returns the received-order audit timeline, including rejected events and their source systems.</summary>
public static class GetTimeline
{
    /// <summary>Maps the timeline route.</summary>
    /// <param name="routes">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapGetTimeline(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/cases/{caseId:guid}/timeline", Handle).WithName("GetCaseTimeline").WithTags("Cases")
            .Produces<IReadOnlyList<TimelineEntryResponse>>().Produces(404).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403);
        return routes;
    }

    /// <summary>Returns audit entries oldest received first; source timestamps are shown separately.</summary>
    /// <param name="caseId">The case identifier.</param>
    /// <param name="reason">Reason required outside the caller's facility.</param>
    /// <param name="context">The request identity.</param>
    /// <param name="db">The read context.</param>
    /// <param name="clock">The access audit clock.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The timeline, 404, or an access refusal.</returns>
    public static async Task<IResult> Handle(Guid caseId, [FromQuery] string? reason, HttpContext context,
        CareLoopDbContext db, IClock clock, CancellationToken ct)
    {
        if (ReadAccess.Identity(context, out var actor) is { } error) return error;
        if (ReadAccess.ValidateReason(reason) is { } invalid) return invalid;
        var @case = await db.Cases.AsNoTracking().SingleOrDefaultAsync(c => c.Id == caseId, ct);
        var outside = actor.Role != DemoRoles.Coordinator && @case is not null && @case.FacilityId != actor.FacilityId;
        var refusal = @case is null ? null : ReadAccess.CasePermission(@case, actor, reason);
        await ReadAccess.AuditAsync(db, actor, clock, refusal is null ? "timeline" : "timeline-denied",
            caseId, outside, reason, null, ct);
        if (refusal is not null) return refusal;
        if (@case is null) return Results.NotFound();
        var entries = await (from audit in db.AuditEvents.AsNoTracking()
            join source in db.SourceEvents.AsNoTracking() on audit.SourceEventId equals source.Id
            where audit.CaseId == caseId
            orderby audit.ReceivedTimestamp, audit.Id
            select new { Audit = audit, source.SourceSystem }).ToListAsync(ct);
        return Results.Ok(entries.OrderBy(e => e.Audit.ReceivedTimestamp)
            .ThenBy(e => CaseProjection.FromJson(e.Audit.NewProjectionJson).Version).Select(e => new TimelineEntryResponse
        {
            AuditEventId = e.Audit.Id, SourceSystem = e.SourceSystem, EventType = e.Audit.EventType,
            Disposition = e.Audit.Disposition, ActorReference = e.Audit.ActorReference, ActorRole = e.Audit.ActorRole,
            SourceTimestamp = e.Audit.SourceTimestamp, ReceivedTimestamp = e.Audit.ReceivedTimestamp,
            PriorStage = e.Audit.PriorProjectionJson is { } prior ? CaseProjection.FromJson(prior).Stage : null,
            NewStage = CaseProjection.FromJson(e.Audit.NewProjectionJson).Stage,
            Reason = e.Audit.Reason, EvidenceReference = e.Audit.EvidenceReference,
        }).ToList());
    }
}

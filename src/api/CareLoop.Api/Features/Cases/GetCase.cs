using CareLoop.Api.Features.Common;
using CareLoop.Api.Infrastructure.Auth;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Contracts.Cases;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Time;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CareLoop.Api.Features.Cases;

/// <summary>Returns one case's coordination projection, without reading through the repository.</summary>
public static class GetCase
{
    /// <summary>Maps the case-detail route.</summary>
    /// <param name="routes">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapGetCase(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/cases/{caseId:guid}", Handle).WithName("GetCase").WithTags("Cases")
            .Produces<CaseDetailResponse>().Produces(404).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403);
        return routes;
    }

    /// <summary>Returns case state only after recording an authorized staff read.</summary>
    /// <param name="caseId">The case identifier.</param>
    /// <param name="reason">Reason required outside the caller's facility.</param>
    /// <param name="context">The request identity.</param>
    /// <param name="db">The no-tracking read context.</param>
    /// <param name="pathways">The display-label configuration.</param>
    /// <param name="clock">The access audit clock.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The case, 404, or an explicit access refusal.</returns>
    public static async Task<IResult> Handle(Guid caseId, [FromQuery] string? reason, HttpContext context,
        CareLoopDbContext db, IPathwayProvider pathways, IClock clock, CancellationToken ct)
    {
        if (ReadAccess.Identity(context, out var actor) is { } error) return error;
        if (ReadAccess.ValidateReason(reason) is { } invalid) return invalid;
        var @case = await db.Cases.AsNoTracking().SingleOrDefaultAsync(c => c.Id == caseId, ct);
        var outside = actor.Role != DemoRoles.Coordinator && @case is not null && @case.FacilityId != actor.FacilityId;
        var refusal = @case is null ? null : ReadAccess.CasePermission(@case, actor, reason);
        await ReadAccess.AuditAsync(db, actor, clock, refusal is null ? "case" : "case-denied",
            caseId, outside, reason, null, ct);
        if (refusal is not null) return refusal;
        return @case is null ? Results.NotFound() : Results.Ok(CaseReadMapping.Detail(@case, actor, pathways));
    }
}

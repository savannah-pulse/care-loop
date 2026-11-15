using CareLoop.Api.Features.Cases;
using CareLoop.Api.Features.Common;
using CareLoop.Api.Infrastructure.Auth;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Contracts.Exceptions;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Time;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CareLoop.Api.Features.Exceptions;

/// <summary>Lists unresolved open exceptions with one owner, next action and deadline per row.</summary>
public static class GetExceptions
{
    /// <summary>Maps the exception-queue route.</summary>
    /// <param name="routes">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapGetExceptions(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/exceptions", Handle).WithName("GetExceptions").WithTags("Exceptions")
            .Produces<IReadOnlyList<ExceptionItemResponse>>().ProducesProblem(400).ProducesProblem(401).ProducesProblem(403);
        return routes;
    }

    /// <summary>Returns unresolved exceptions, earliest due first, within an audited facility scope.</summary>
    /// <param name="facilityId">Facility filter; defaults to the staff reader's facility.</param>
    /// <param name="ownerReference">Optional accountable owner.</param>
    /// <param name="reason">Reason required outside the reader's facility.</param>
    /// <param name="context">The staff identity.</param>
    /// <param name="db">The read context.</param>
    /// <param name="pathways">The display-label configuration.</param>
    /// <param name="clock">The overdue and read-audit clock.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The exception queue or an explicit refusal.</returns>
    public static async Task<IResult> Handle([FromQuery] string? facilityId, [FromQuery] string? ownerReference,
        [FromQuery] string? reason, HttpContext context, CareLoopDbContext db, IPathwayProvider pathways,
        IClock clock, CancellationToken ct)
    {
        if (ReadAccess.Identity(context, out var actor) is { } error) return error;
        if (ReadAccess.ValidateReason(reason) is { } invalid) return invalid;
        var queryText = ReadAccess.Query(("facilityId", facilityId ?? actor.FacilityId), ("ownerReference", ownerReference));
        if (queryText.Length > 500 || facilityId?.Length > 100 || ownerReference?.Length > 200
            || facilityId is not null && string.IsNullOrWhiteSpace(facilityId)
            || ownerReference is not null && string.IsNullOrWhiteSpace(ownerReference))
            return Results.Problem("Invalid exception filter.", statusCode: 400);
        var outside = actor.Role != DemoRoles.Coordinator && facilityId is not null && facilityId != actor.FacilityId;
        var refusal = ReadAccess.CrossFacilityPermission(actor, facilityId, reason);
        if (refusal is not null)
        {
            await ReadAccess.AuditAsync(db, actor, clock, "exceptions-denied", null, outside, reason, queryText, ct);
            return refusal;
        }
        var query = ReadAccess.FacilityScope(ReadAccess.RoleScope(db.Cases.AsNoTracking(), actor), actor, facilityId)
            .Where(c => c.Stage != CaseStage.Closed && c.Exceptions != CaseExceptions.None);
        if (ownerReference is not null) query = query.Where(c => c.Owner.Reference == ownerReference);
        var cases = await query.OrderBy(c => c.DueAt).ThenBy(c => c.Id).ToListAsync(ct);
        var response = cases.Select(c => CaseReadMapping.Exception(c, actor, pathways, clock.UtcNow)).ToList();
        await ReadAccess.AuditAsync(db, actor, clock, "exceptions", null, outside, reason, queryText, ct);
        return Results.Ok(response);
    }
}

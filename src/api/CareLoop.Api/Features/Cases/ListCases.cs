using CareLoop.Api.Features.Common;
using CareLoop.Api.Infrastructure.Auth;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Contracts.Cases;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Time;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CareLoop.Api.Features.Cases;

/// <summary>Lists dashboard cases using audited, no-tracking, role-scoped reads.</summary>
public static class ListCases
{
    /// <summary>Maps the case-list route.</summary>
    /// <param name="routes">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapListCases(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/cases", Handle).WithName("ListCases").WithTags("Cases")
            .Produces<IReadOnlyList<CaseDetailResponse>>().ProducesProblem(400).ProducesProblem(401).ProducesProblem(403);
        return routes;
    }

    /// <summary>Lists matching cases, updated newest first, with bounded paging.</summary>
    /// <param name="stage">Optional stage.</param>
    /// <param name="ownerReference">Optional accountable owner.</param>
    /// <param name="escalated">Optional open-escalation filter.</param>
    /// <param name="skip">Nonnegative offset; default zero.</param>
    /// <param name="take">Page size from 1 to 200; default 50.</param>
    /// <param name="facilityId">Facility filter; defaults to the staff reader's facility.</param>
    /// <param name="patientReference">Patient search, restricted to clinician and receiving-clinician.</param>
    /// <param name="reason">Reason required for a cross-facility read.</param>
    /// <param name="context">The caller-stated demo identity.</param>
    /// <param name="db">The read context.</param>
    /// <param name="pathways">The display-label rules.</param>
    /// <param name="clock">The access audit clock.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Matching rows, or an explicit validation/access refusal.</returns>
    public static async Task<IResult> Handle([FromQuery] CaseStage? stage, [FromQuery] string? ownerReference,
        [FromQuery] bool? escalated, [FromQuery] int? skip, [FromQuery] int? take,
        [FromQuery] string? facilityId, [FromQuery] string? patientReference, [FromQuery] string? reason,
        HttpContext context, CareLoopDbContext db, IPathwayProvider pathways, IClock clock, CancellationToken ct)
    {
        if (ReadAccess.Identity(context, out var actor) is { } identityError) return identityError;
        if (ReadAccess.ValidateReason(reason) is { } reasonError) return reasonError;
        if (skip < 0 || take is < 1 or > 200 || stage is { } value && !Enum.IsDefined(value)
            || ownerReference?.Length > 200 || facilityId?.Length > 100 || patientReference?.Length > 200
            || ownerReference is not null && string.IsNullOrWhiteSpace(ownerReference)
            || facilityId is not null && string.IsNullOrWhiteSpace(facilityId)
            || patientReference is not null && string.IsNullOrWhiteSpace(patientReference))
            return Results.Problem("Invalid case filter or paging bounds.", statusCode: 400);
        var queryText = ReadAccess.Query(("stage", stage), ("ownerReference", ownerReference), ("escalated", escalated),
            ("skip", skip ?? 0), ("take", take ?? 50), ("facilityId", facilityId ?? actor.FacilityId), ("patientReference", patientReference));
        if (queryText.Length > 500) return Results.Problem("The encoded query exceeds 500 characters.", statusCode: 400);
        var outside = actor.Role != DemoRoles.Coordinator && facilityId is not null && facilityId != actor.FacilityId;
        var refusal = patientReference is not null && actor.Role is not (DemoRoles.Clinician or DemoRoles.ReceivingClinician)
            ? Results.Problem("Patient searches are restricted to clinician roles.", statusCode: 403)
            : ReadAccess.CrossFacilityPermission(actor, facilityId, reason);
        if (refusal is not null)
        {
            await ReadAccess.AuditAsync(db, actor, clock, "case-list-denied", null, outside, reason, queryText, ct);
            return refusal;
        }

        var query = ReadAccess.FacilityScope(ReadAccess.RoleScope(db.Cases.AsNoTracking(), actor), actor, facilityId);
        if (stage is not null) query = query.Where(c => c.Stage == stage);
        if (ownerReference is not null) query = query.Where(c => c.Owner.Reference == ownerReference);
        if (escalated is not null) query = query.Where(c => c.IsEscalated == escalated);
        if (patientReference is not null) query = query.Where(c => c.PatientReference == patientReference);
        var cases = await query.OrderByDescending(c => c.UpdatedAt).ThenBy(c => c.Id)
            .Skip(skip ?? 0).Take(take ?? 50).ToListAsync(ct);
        var response = await CaseReadMapping.DetailsAsync(cases, actor, pathways, db, ct);
        await ReadAccess.AuditAsync(db, actor, clock, "case-list", null, outside, reason, queryText, ct);
        return Results.Ok(response);
    }
}

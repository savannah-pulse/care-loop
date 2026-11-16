using CareLoop.Api.Infrastructure.Auth;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Api.Infrastructure.Persistence.Audit;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Time;

namespace CareLoop.Api.Features.Common;

/// <summary>Shared demo identity validation, facility scoping and accountable read logging.</summary>
/// <remarks>Facility is the current team boundary. Real authentication must replace these caller-stated headers before production.</remarks>
internal static class ReadAccess
{
    /// <summary>Validates the caller-stated staff identity.</summary>
    /// <param name="context">The HTTP request.</param>
    /// <param name="actor">The valid identity, when available.</param>
    /// <returns>A refusal, or null when valid.</returns>
    public static IResult? Identity(HttpContext context, out Actor actor)
    {
        actor = null!;
        var user = Header(context, DemoRoles.UserHeader);
        var role = Header(context, DemoRoles.RoleHeader)?.ToLowerInvariant();
        var facility = Header(context, DemoRoles.FacilityHeader);
        if (user is null || role is null)
            return Results.Problem("Staff identity headers are required.", statusCode: StatusCodes.Status401Unauthorized);
        if (user.Length > 200 || role.Length > 100 || facility?.Length > 100)
            return Results.Problem("Identity headers exceed their permitted length.", statusCode: StatusCodes.Status400BadRequest);
        if (!DemoRoles.HumanRoles.Contains(role))
            return Results.Problem("This role may not read case data.", statusCode: StatusCodes.Status403Forbidden);
        if (role != DemoRoles.Coordinator && facility is null)
            return Results.Problem("A staff facility header is required.", statusCode: StatusCodes.Status400BadRequest);
        actor = new Actor { Reference = user, Role = role, FacilityId = facility };
        return null;
    }

    /// <summary>Validates an optional documented access reason.</summary>
    /// <param name="reason">The caller's reason.</param>
    /// <returns>A validation refusal, or null.</returns>
    public static IResult? ValidateReason(string? reason) =>
        reason?.Length > 1000
            ? Results.Problem("The access reason must not exceed 1000 characters.", statusCode: 400) : null;

    /// <summary>Restricts coordinator reads to open escalations addressed to their role.</summary>
    /// <param name="query">The case query.</param>
    /// <param name="actor">The validated staff identity.</param>
    /// <returns>The role-scoped query.</returns>
    public static IQueryable<Case> RoleScope(IQueryable<Case> query, Actor actor) =>
        actor.Role == DemoRoles.Coordinator
            ? query.Where(c => c.Escalations.Any(e => e.ResolvedAt == null && e.EscalatedTo.Role == DemoRoles.Coordinator))
            : query;

    /// <summary>Applies the default facility or an explicitly requested cross-facility scope.</summary>
    /// <param name="query">The role-scoped query.</param>
    /// <param name="actor">The staff identity.</param>
    /// <param name="facilityId">An explicit facility, if requested.</param>
    /// <returns>The scoped query.</returns>
    public static IQueryable<Case> FacilityScope(IQueryable<Case> query, Actor actor, string? facilityId)
    {
        var facility = facilityId ?? (actor.Role == DemoRoles.Coordinator ? null : actor.FacilityId);
        return facility is null ? query : query.Where(c => c.FacilityId == facility);
    }

    /// <summary>Checks a particular case's role and facility access.</summary>
    /// <param name="case">The case projection.</param>
    /// <param name="actor">The staff identity.</param>
    /// <param name="reason">The access reason, if supplied.</param>
    /// <returns>A refusal, or null when allowed.</returns>
    public static IResult? CasePermission(Case @case, Actor actor, string? reason)
    {
        if (actor.Role == DemoRoles.Coordinator)
            return @case.Escalations.Any(e => e.IsOpen && e.EscalatedTo.Role == DemoRoles.Coordinator)
                ? null : Results.Problem("A coordinator may read only cases escalated to that role.", statusCode: 403);
        return CrossFacilityPermission(actor, @case.FacilityId, reason);
    }

    /// <summary>Requires a reason before a staff read outside their facility.</summary>
    /// <param name="actor">The staff identity.</param>
    /// <param name="facilityId">The requested facility.</param>
    /// <param name="reason">The access reason.</param>
    /// <returns>A refusal, or null when allowed.</returns>
    public static IResult? CrossFacilityPermission(Actor actor, string? facilityId, string? reason) =>
        actor.Role != DemoRoles.Coordinator && facilityId is not null && facilityId != actor.FacilityId
            && string.IsNullOrWhiteSpace(reason)
                ? Results.Problem("Reading outside your facility requires a documented reason.", statusCode: 403)
                : null;

    /// <summary>Appends one read record before returning any data; a persistence failure fails the request.</summary>
    /// <param name="db">The read context.</param>
    /// <param name="actor">Who looked.</param>
    /// <param name="clock">Supplies the read time.</param>
    /// <param name="view">The stable view key; denied attempts append -denied.</param>
    /// <param name="caseId">The case, or null for a list.</param>
    /// <param name="outsideTeam">Whether a cross-facility read was requested.</param>
    /// <param name="reason">The supplied reason.</param>
    /// <param name="query">The request filters.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>Completion of the append-only access audit.</returns>
    public static async Task AuditAsync(CareLoopDbContext db, Actor actor, IClock clock, string view,
        Guid? caseId, bool outsideTeam, string? reason, string? query, CancellationToken ct)
    {
        db.CaseReads.Add(new CaseReadRecord
        {
            Id = Guid.NewGuid(), CaseId = caseId, ActorReference = actor.Reference, ActorRole = actor.Role,
            ActorFacilityId = actor.FacilityId, View = view, OutsideTeam = outsideTeam,
            Reason = reason, Query = query, ReadAt = clock.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Serializes filter names and values for the bounded read-audit query field.</summary>
    /// <param name="filters">The query filters.</param>
    /// <returns>The encoded filters.</returns>
    public static string Query(params (string Key, object? Value)[] filters) =>
        string.Join("&", filters.Where(f => f.Value is not null)
            .Select(f => $"{f.Key}={Uri.EscapeDataString(f.Value!.ToString()!)}"));

    private static string? Header(HttpContext context, string name) =>
        context.Request.Headers.TryGetValue(name, out var values) && values.Count == 1
            && !string.IsNullOrWhiteSpace(values[0]) ? values[0]!.Trim() : null;
}

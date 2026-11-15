using CareLoop.Api.Features.Common;
using CareLoop.Contracts.Programmes;
using CareLoop.Domain.Pathways;

namespace CareLoop.Api.Features.Programmes;

/// <summary>Exposes read-only pathway rules with citations, clinical approval status and change history.</summary>
public static class GetProgrammes
{
    /// <summary>Maps list and detail programme routes.</summary>
    /// <param name="routes">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapGetProgrammes(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/programmes", List).WithName("ListProgrammes").WithTags("Programmes")
            .Produces<IReadOnlyList<ProgrammeResponse>>().ProducesProblem(400).ProducesProblem(401).ProducesProblem(403);
        routes.MapGet("/programmes/{pathwayId}", Get).WithName("GetProgramme").WithTags("Programmes")
            .Produces<ProgrammeResponse>().Produces(404).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403);
        return routes;
    }

    /// <summary>Lists the configured programmes for an identified staff reader.</summary>
    /// <param name="context">The staff identity.</param>
    /// <param name="pathways">The current definitions.</param>
    /// <returns>The programme list or an identity refusal.</returns>
    public static IResult List(HttpContext context, IPathwayProvider pathways) =>
        ReadAccess.Identity(context, out _) ?? Results.Ok(pathways.GetAll().OrderBy(p => p.Id).Select(Map).ToList());

    /// <summary>Returns a programme without assuming an unknown key exists.</summary>
    /// <param name="pathwayId">The programme key.</param>
    /// <param name="context">The staff identity.</param>
    /// <param name="pathways">The current definitions.</param>
    /// <returns>The programme, 404, or an identity refusal.</returns>
    public static IResult Get(string pathwayId, HttpContext context, IPathwayProvider pathways)
    {
        if (ReadAccess.Identity(context, out _) is { } error) return error;
        var definition = pathways.GetAll().FirstOrDefault(p => p.Id == pathwayId);
        return definition is null ? Results.NotFound() : Results.Ok(Map(pathways.Get(pathwayId)));
    }

    private static ProgrammeResponse Map(PathwayDefinition p) => new()
    {
        PathwayId = p.Id, Version = p.Version, DisplayName = p.DisplayName, IsPlaceholder = p.IsPlaceholder,
        UrgencyCodes = p.UrgencyCodes, UnassignedQueueRole = p.UnassignedQueueRole, Stages = p.Stages,
        Tracks = p.Tracks, Deadlines = p.Deadlines, EscalationLadder = p.EscalationLadder,
        ClosureRules = p.ClosureRules, EvidenceKinds = p.EvidenceKinds, ChangeLog = p.ChangeLog,
    };
}

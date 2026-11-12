using CareLoop.Application.Queries;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Stages;

namespace CareLoop.Api.Endpoints;

/// <summary>Read models for the worklist, case view, timeline, and exception queues. Read-only.</summary>
public sealed class CaseReadEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Cases");

        group.MapGet("/cases", ListCasesAsync)
            .WithName("ListCases")
            .WithSummary("Paged worklist. stage accepts a stage code such as OWNER_ASSIGNED.");
        group.MapGet("/cases/{id:guid}", GetCaseAsync).WithName("GetCase");
        group.MapGet("/cases/{id:guid}/timeline", GetTimelineAsync).WithName("GetCaseTimeline");
        group.MapGet("/exceptions", ListExceptionsAsync)
            .WithName("ListExceptions")
            .WithSummary("Paged exception queue. Open exceptions by default; pass status=resolved or all=true for others.");
    }

    private static async Task<IResult> ListCasesAsync(
        ICaseQueries queries, CancellationToken ct,
        string? stage = null, bool? hasOpenExceptions = null, OwnerKind? ownerKind = null,
        string? ownerId = null, int page = 1, int pageSize = 25)
    {
        CaseStage? stageFilter = null;
        if (!string.IsNullOrWhiteSpace(stage))
        {
            var match = StageCatalog.All.FirstOrDefault(s => s.Code.Equals(stage, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["stage"] = [$"Unknown stage '{stage}'. Use one of: {string.Join(", ", StageCatalog.All.Select(s => s.Code))}."],
                });
            }

            stageFilter = match.Stage;
        }

        var result = await queries.ListCasesAsync(
            new CaseListQuery(stageFilter, hasOpenExceptions, ownerKind, ownerId, page, pageSize), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetCaseAsync(Guid id, ICaseQueries queries, CancellationToken ct) =>
        await queries.GetCaseAsync(id, ct) is { } found ? Results.Ok(found) : Results.NotFound();

    private static async Task<IResult> GetTimelineAsync(Guid id, ICaseQueries queries, CancellationToken ct) =>
        await queries.GetTimelineAsync(id, ct) is { } timeline ? Results.Ok(timeline) : Results.NotFound();

    private static async Task<IResult> ListExceptionsAsync(
        ICaseQueries queries, CancellationToken ct,
        ExceptionStatus? status = null, bool all = false, int page = 1, int pageSize = 25) =>
        Results.Ok(await queries.ListExceptionsAsync(
            new ExceptionListQuery(all ? null : status ?? ExceptionStatus.Open, page, pageSize), ct));
}

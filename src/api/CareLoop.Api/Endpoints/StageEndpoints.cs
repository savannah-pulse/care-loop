using CareLoop.Domain.Stages;

namespace CareLoop.Api.Endpoints;

public sealed class StageEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app) =>
        app.MapGet("/api/stages", () => Results.Ok(StageCatalog.All.Select(s =>
                new StageResponse(s.Number, s.Code, s.EntryEventType, s.Description))))
            .WithName("ListStages")
            .WithTags("Stages")
            .WithSummary("The ten sequential case stages and the event that enters each.");

    private sealed record StageResponse(int Number, string Code, string EntryEventType, string Description);
}

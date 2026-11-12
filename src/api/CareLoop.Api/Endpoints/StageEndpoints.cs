using CareLoop.Domain.Stages;

namespace CareLoop.Api.Endpoints;

public sealed class StageEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app) =>
        app.MapGet("/api/stages", () => Results.Ok(StageCatalog.All.Select(s =>
                new StageResponse(s.Number, s.Code, s.EntryEventType, s.Driver, s.Description))))
            .WithName("ListStages")
            .WithTags("Stages")
            .WithSummary("The ten sequential case stages, the event that enters each, and the component that drives it.");

    private sealed record StageResponse(int Number, string Code, string EntryEventType, StageDriver Driver, string Description);
}

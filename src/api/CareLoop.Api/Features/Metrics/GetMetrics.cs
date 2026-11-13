using CareLoop.Api.Features.Common;
using CareLoop.Api.Infrastructure.Auth;
using CareLoop.Contracts.Cases;
using CareLoop.Contracts.Common;
using CareLoop.Contracts.Exceptions;
using CareLoop.Contracts.Metrics;
using CareLoop.Contracts.Summaries;
using CareLoop.Domain.Cases;
using Microsoft.AspNetCore.Mvc;

namespace CareLoop.Api.Features.Metrics;

/// <summary>
/// Slice for <c>GET /metrics</c>: loop-closure metrics.
/// </summary>
/// <remarks>
/// Will aggregate coordination measures (cases by stage and exception, overdue, escalated, unassigned, acknowledgement and closure rates, median time to closure) with no-tracking queries. These are not clinical outcomes.
/// </remarks>
public static class GetMetrics
{
    /// <summary>
    /// Maps the route for this slice.
    /// </summary>
    /// <param name="routes">The application's route builder.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapGetMetrics(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/metrics", Handle)
            .WithName("GetMetrics")
            .WithTags("Metrics")
            .Produces<MetricsResponse>()
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented);
        return routes;
    }

    /// <summary>
    /// Returns the current loop-closure metrics.
    /// </summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>200 with the current metrics.</returns>
    public static IResult Handle(CancellationToken ct)
    {
        // TODO(WS-6): aggregate the case table and audit trail into MetricsResponse; take the time from IClock.
        return NotImplementedResults.For("WS6", "GetMetrics");
    }
}

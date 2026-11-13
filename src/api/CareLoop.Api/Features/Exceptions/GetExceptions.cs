using CareLoop.Api.Features.Common;
using CareLoop.Api.Infrastructure.Auth;
using CareLoop.Contracts.Cases;
using CareLoop.Contracts.Common;
using CareLoop.Contracts.Exceptions;
using CareLoop.Contracts.Metrics;
using CareLoop.Contracts.Summaries;
using CareLoop.Domain.Cases;
using Microsoft.AspNetCore.Mvc;

namespace CareLoop.Api.Features.Exceptions;

/// <summary>
/// Slice for <c>GET /exceptions</c>: the exception queue.
/// </summary>
/// <remarks>
/// A case appears in the queue only while it has an unresolved exception (overdue, rejected, unreachable, unacknowledged or unassigned). Each row shows the single next action and who is accountable for it, because an exception never parks a case.
/// </remarks>
public static class GetExceptions
{
    /// <summary>
    /// Maps the route for this slice.
    /// </summary>
    /// <param name="routes">The application's route builder.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapGetExceptions(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/exceptions", Handle)
            .WithName("GetExceptions")
            .WithTags("Exceptions")
            .Produces<IReadOnlyList<ExceptionItemResponse>>()
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented);
        return routes;
    }

    /// <summary>
    /// Lists the cases that currently need attention.
    /// </summary>
    /// <param name="facilityId">Only cases of this facility; all facilities when omitted.</param>
    /// <param name="ownerReference">Only cases owned by this person or queue; all owners when omitted.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>200 with the open exceptions, most overdue first.</returns>
    public static IResult Handle([FromQuery] string? facilityId, [FromQuery] string? ownerReference, CancellationToken ct)
    {
        // TODO(WS-6): query open cases whose Exceptions flag is not None, order by DueAt, project to ExceptionItemResponse.
        return NotImplementedResults.For("WS6", "GetExceptions");
    }
}

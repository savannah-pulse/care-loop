using CareLoop.Api.Features.Common;
using CareLoop.Api.Infrastructure.Auth;
using CareLoop.Contracts.Cases;
using CareLoop.Contracts.Common;
using CareLoop.Contracts.Exceptions;
using CareLoop.Contracts.Metrics;
using CareLoop.Contracts.Summaries;
using CareLoop.Domain.Cases;
using Microsoft.AspNetCore.Mvc;

namespace CareLoop.Api.Features.Cases;

/// <summary>
/// Slice for <c>GET /cases/{caseId}</c>: the full coordination state of one case.
/// </summary>
/// <remarks>
/// Reads go direct: will query <c>CareLoopDbContext</c> with a no-tracking projection. Shows the single accountable owner and, while a handoff is pending, the proposed owner alongside.
/// </remarks>
public static class GetCase
{
    /// <summary>
    /// Maps the route for this slice.
    /// </summary>
    /// <param name="routes">The application's route builder.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapGetCase(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/cases/{caseId:guid}", Handle)
            .WithName("GetCase")
            .WithTags("Cases")
            .Produces<CaseDetailResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented);
        return routes;
    }

    /// <summary>
    /// Returns one case.
    /// </summary>
    /// <param name="caseId">CareLoop&apos;s identifier of the case.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>200 with the case, or 404 when no case has that identifier.</returns>
    public static IResult Handle(Guid caseId, CancellationToken ct)
    {
        // TODO(WS-6): load the case no-tracking and project to CaseDetailResponse including the four tracks.
        return NotImplementedResults.For("WS6", "GetCase");
    }
}

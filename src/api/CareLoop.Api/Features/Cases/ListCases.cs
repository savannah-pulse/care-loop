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
/// Slice for <c>GET /cases</c>: the list of cases shown in the dashboard.
/// </summary>
/// <remarks>
/// Reads go direct: will query <c>CareLoopDbContext</c> with a no-tracking projection, never through the repository. Returns coordination data only (references, stage, owner, deadline), no clinical data.
/// </remarks>
public static class ListCases
{
    /// <summary>
    /// Maps the route for this slice.
    /// </summary>
    /// <param name="routes">The application's route builder.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapListCases(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/cases", Handle)
            .WithName("ListCases")
            .WithTags("Cases")
            .Produces<IReadOnlyList<CaseSummaryResponse>>()
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented);
        return routes;
    }

    /// <summary>
    /// Lists cases, optionally filtered.
    /// </summary>
    /// <param name="stage">Only cases in this stage; all stages when omitted.</param>
    /// <param name="ownerReference">Only cases owned by this person or queue; all owners when omitted.</param>
    /// <param name="escalated">Only cases with (or without) an open escalation; both when omitted.</param>
    /// <param name="skip">Number of cases to skip, for paging; zero when omitted.</param>
    /// <param name="take">Maximum number of cases to return; a server default when omitted.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>200 with the matching cases, most recently updated first.</returns>
    public static IResult Handle([FromQuery] CaseStage? stage, [FromQuery] string? ownerReference, [FromQuery] bool? escalated, [FromQuery] int? skip, [FromQuery] int? take, CancellationToken ct)
    {
        // TODO(WS-6): filter, order by UpdatedAt descending, page and project to CaseSummaryResponse.
        return NotImplementedResults.For("WS6", "ListCases");
    }
}

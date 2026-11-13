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
/// Slice for <c>GET /cases/{caseId}/timeline</c>: the audit trail of one case.
/// </summary>
/// <remarks>
/// Will return every audit event of the case in the order CareLoop received them, including refused attempts such as a closure without evidence, so the timeline shows who did what, in which role and on what evidence.
/// </remarks>
public static class GetTimeline
{
    /// <summary>
    /// Maps the route for this slice.
    /// </summary>
    /// <param name="routes">The application's route builder.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapGetTimeline(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/cases/{caseId:guid}/timeline", Handle)
            .WithName("GetCaseTimeline")
            .WithTags("Cases")
            .Produces<IReadOnlyList<TimelineEntryResponse>>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented);
        return routes;
    }

    /// <summary>
    /// Returns the audit timeline of one case.
    /// </summary>
    /// <param name="caseId">CareLoop&apos;s identifier of the case.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>200 with the timeline entries oldest first, or 404 when no case has that identifier.</returns>
    public static IResult Handle(Guid caseId, CancellationToken ct)
    {
        // TODO(WS-6): query AuditEvents for the case ordered by ReceivedTimestamp and project to TimelineEntryResponse.
        return NotImplementedResults.For("WS6", "GetTimeline");
    }
}

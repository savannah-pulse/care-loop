using CareLoop.Api.Features.Common;
using CareLoop.Api.Infrastructure.Auth;
using CareLoop.Contracts.Common;
using CareLoop.Contracts.Summaries;
using Microsoft.AspNetCore.Mvc;

namespace CareLoop.Api.Features.Summaries;

/// <summary>
/// Slice for <c>POST /cases/{caseId}/handoff-summary/{summaryId}/rejection</c>: a person rejects a drafted summary.
/// </summary>
/// <remarks>
/// Without this, a draft nobody accepted is indistinguishable from a draft nobody has read yet,
/// and the evaluation question "how often did a reviewer reject what AI wrote" has no answer. The
/// rejection names the reviewer and a reason, and the case screen can then offer a fresh draft.
/// <para>
/// Rejection is not a case event: it changes no stage, owner or deadline, so it is stored on the
/// summary record rather than through <c>ICaseRepository.SaveAsync</c>. Only approval, which puts
/// a person's name to text that leaves CareLoop, is a source event.
/// </para>
/// </remarks>
public static class RejectSummary
{
    /// <summary>
    /// Maps the route for this slice.
    /// </summary>
    /// <param name="routes">The application's route builder.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapRejectSummary(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/cases/{caseId:guid}/handoff-summary/{summaryId:guid}/rejection", Handle)
            .WithName("RejectHandoffSummary")
            .WithTags("Summaries")
            .Produces<HandoffSummaryResponse>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented);
        return routes;
    }

    /// <summary>
    /// Rejects a drafted handoff summary.
    /// </summary>
    /// <param name="caseId">The case the summary belongs to.</param>
    /// <param name="summaryId">The draft being rejected.</param>
    /// <param name="request">Why the reviewer rejected it.</param>
    /// <param name="userReference">Reference of the rejecting person, from the demo identity header.</param>
    /// <param name="role">Role of the rejecting person, from the demo identity header.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>200 with the rejected summary; 403 when the caller has no role allowed to review; 404 when the case or draft does not exist; 409 when it is already approved or rejected.</returns>
    public static IResult Handle(Guid caseId, Guid summaryId, RejectSummaryRequest request, [FromHeader(Name = DemoRoles.UserHeader)] string? userReference, [FromHeader(Name = DemoRoles.RoleHeader)] string? role, CancellationToken ct)
    {
        // TODO(WS-9): check the role, mark the record Rejected with the reviewer and reason, and record nothing on the case.
        return NotImplementedResults.For("WS9", "RejectSummary");
    }
}

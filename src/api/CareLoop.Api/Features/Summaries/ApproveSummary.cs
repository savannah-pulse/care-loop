using CareLoop.Api.Features.Common;
using CareLoop.Api.Infrastructure.Auth;
using CareLoop.Contracts.Cases;
using CareLoop.Contracts.Common;
using CareLoop.Contracts.Exceptions;
using CareLoop.Contracts.Metrics;
using CareLoop.Contracts.Summaries;
using CareLoop.Domain.Cases;
using Microsoft.AspNetCore.Mvc;

namespace CareLoop.Api.Features.Summaries;

/// <summary>
/// Slice for <c>POST /cases/{caseId}/handoff-summary/{summaryId}/approval</c>: a person approves a drafted summary.
/// </summary>
/// <remarks>
/// The approval is an event: will record a <c>HandoffSummaryApproved</c> source event through the single write path, naming the approver from the demo identity headers, so the audit trail shows who took responsibility for the text. Approval changes nothing else: it cannot set urgency, alter source data, close the case or send a message.
/// </remarks>
public static class ApproveSummary
{
    /// <summary>
    /// Maps the route for this slice.
    /// </summary>
    /// <param name="routes">The application's route builder.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapApproveSummary(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/cases/{caseId:guid}/handoff-summary/{summaryId:guid}/approval", Handle)
            .WithName("ApproveHandoffSummary")
            .WithTags("Summaries")
            .Produces<HandoffSummaryResponse>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented);
        return routes;
    }

    /// <summary>
    /// Approves a drafted handoff summary.
    /// </summary>
    /// <param name="caseId">The case the summary belongs to.</param>
    /// <param name="summaryId">The draft being approved.</param>
    /// <param name="request">The text being approved, which may be an edited version of the draft.</param>
    /// <param name="userReference">Reference of the approving person, from the demo identity header.</param>
    /// <param name="role">Role of the approving person, from the demo identity header.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>200 with the approved summary; 403 when the caller has no role allowed to approve; 404 when the case or draft does not exist; 409 when it is already approved.</returns>
    public static IResult Handle(Guid caseId, Guid summaryId, ApproveSummaryRequest request, [FromHeader(Name = DemoRoles.UserHeader)] string? userReference, [FromHeader(Name = DemoRoles.RoleHeader)] string? role, CancellationToken ct)
    {
        // TODO(WS-9): check the role, mark the record Approved and save a HandoffSummaryApproved event via ICaseRepository.SaveAsync.
        return NotImplementedResults.For("WS9", "ApproveSummary");
    }
}

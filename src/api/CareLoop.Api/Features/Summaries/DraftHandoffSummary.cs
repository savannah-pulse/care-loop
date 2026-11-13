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
/// Slice for <c>POST /cases/{caseId}/handoff-summary</c>: asks AI to draft a handoff summary.
/// </summary>
/// <remarks>
/// AI output is a draft. Will build a request from coordination facts only (stage, owner, next action, deadline, timeline), call <c>IAiProvider</c>, and store the result as a draft that is never sent or published until a person approves it. With the no-op provider the endpoint answers 409 because AI is switched off.
/// </remarks>
public static class DraftHandoffSummary
{
    /// <summary>
    /// Maps the route for this slice.
    /// </summary>
    /// <param name="routes">The application's route builder.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapDraftHandoffSummary(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/cases/{caseId:guid}/handoff-summary", Handle)
            .WithName("DraftHandoffSummary")
            .WithTags("Summaries")
            .Produces<HandoffSummaryResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented);
        return routes;
    }

    /// <summary>
    /// Drafts a handoff summary for a case.
    /// </summary>
    /// <param name="caseId">The case to summarise.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>201 with the draft; 404 when the case does not exist; 409 when AI is switched off.</returns>
    public static IResult Handle(Guid caseId, CancellationToken ct)
    {
        // TODO(WS-9): load the case and timeline no-tracking, call IAiProvider.DraftHandoffSummaryAsync, store a HandoffSummaryRecord in status Draft.
        return NotImplementedResults.For("WS9", "DraftHandoffSummary");
    }
}

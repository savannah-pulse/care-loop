using CareLoop.Api.Features.Common;
using CareLoop.Contracts.Common;
using CareLoop.Contracts.Summaries;

namespace CareLoop.Api.Features.Summaries;

/// <summary>
/// Slice for <c>POST /cases/{caseId}/handoff-summary</c>: asks AI to draft a handoff summary.
/// </summary>
/// <remarks>
/// AI output is a draft. Will collect the eligible facts, call the configured provider through
/// <see cref="HandoffSummaryDrafter"/> and store the result as a draft that is never sent or
/// published until a person approves it. With the no-op provider the endpoint answers 409 because
/// AI is switched off; when the provider fails it answers 503, because "no draft" and "a draft
/// that may be wrong" must never look the same to the person reading the screen.
/// <para>
/// A draft may already exist for the case, produced when the handoff was proposed (decision 11).
/// Asking again replaces an unapproved draft and is refused once one is approved.
/// </para>
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
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .Produces<NotImplementedResponse>(StatusCodes.Status501NotImplemented);
        return routes;
    }

    /// <summary>
    /// Drafts a handoff summary for a case.
    /// </summary>
    /// <param name="caseId">The case to summarise.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>201 with the draft; 404 when the case does not exist; 409 when AI is switched off or the case already has an approved summary; 503 when the provider could not produce a valid draft.</returns>
    public static IResult Handle(Guid caseId, CancellationToken ct)
    {
        // TODO(WS-9): load the case, call HandoffSummaryDrafter.DraftAsync with trigger Requested,
        // and map AiDraftUnavailableException to 503 carrying the reason.
        return NotImplementedResults.For("WS9", "DraftHandoffSummary");
    }
}

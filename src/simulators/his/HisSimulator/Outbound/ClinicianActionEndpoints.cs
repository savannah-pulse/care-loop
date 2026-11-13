using Microsoft.AspNetCore.Http.HttpResults;

namespace HisSimulator.Outbound;

/// <summary>
/// Maps the manual "act as clinician" routes, which let a person drive the simulated HIS by hand.
/// </summary>
/// <remarks>
/// Each action is sent through <see cref="OutboundEventSender"/> exactly like a scenario step, so a
/// manual action reaches CareLoop only as FHIR. The actions stand in for a human in the hospital
/// system; nothing here lets the simulator decide urgency or closure on its own.
/// </remarks>
public static class ClinicianActionEndpoints
{
    /// <summary>
    /// Maps <c>GET /cases</c> and the four <c>POST /actions/...</c> routes.
    /// </summary>
    /// <param name="app">The route builder to add the endpoints to.</param>
    /// <returns>The same <paramref name="app"/> instance, for chaining.</returns>
    public static IEndpointRouteBuilder MapClinicianActionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/cases", ListCases)
            .WithName("ListSimulatedCases")
            .Produces<SimulatedCaseListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status501NotImplemented);

        var actions = app.MapGroup("/actions");

        actions.MapPost("/clinical-review", RecordClinicalReview)
            .WithName("RecordClinicalReview")
            .Produces<OutboundEventResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status501NotImplemented);

        actions.MapPost("/referral-decision", DecideReferral)
            .WithName("DecideReferral")
            .Produces<OutboundEventResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status501NotImplemented);

        actions.MapPost("/outcome", RecordOutcome)
            .WithName("RecordOutcome")
            .Produces<OutboundEventResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status501NotImplemented);

        actions.MapPost("/closure", AuthorizeClosure)
            .WithName("AuthorizeClosure")
            .Produces<OutboundEventResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status501NotImplemented);

        return app;
    }

    /// <summary>
    /// Will list the simulated cases so the ClientApp can offer them for manual actions.
    /// </summary>
    /// <returns>501 until implemented; then 200 with a <see cref="SimulatedCaseListResponse"/>.</returns>
    private static ProblemHttpResult ListCases()
    {
        // TODO(WS-7): return SimulatorStore.GetCases() wrapped in SimulatedCaseListResponse.
        return StubResults.NotImplemented("ClinicianActionEndpoints.ListCases");
    }

    /// <summary>
    /// Will send a completed review <c>Task</c> carrying the reviewed urgency.
    /// </summary>
    /// <param name="request">The case, the reviewing clinician and the placeholder urgency code.</param>
    /// <returns>501 until implemented; then 200 with the send result, 400 for an invalid request or 404 for an unknown case.</returns>
    private static ProblemHttpResult RecordClinicalReview(ClinicalReviewActionRequest request)
    {
        // TODO(WS-7): validate (SYN- performer id, non-empty urgency), map to an OutboundEventRequest of kind
        // ClinicalReview stamped with ISimulatorClock.UtcNow and a new source event key, send, return the result.
        return StubResults.NotImplemented("ClinicianActionEndpoints.RecordClinicalReview");
    }

    /// <summary>
    /// Will send the referral <c>Task</c> as accepted or rejected.
    /// </summary>
    /// <param name="request">The case, the deciding clinician, the decision and its reason.</param>
    /// <returns>501 until implemented; then 200 with the send result, 400 for an invalid request or 404 for an unknown case.</returns>
    private static ProblemHttpResult DecideReferral(ReferralDecisionActionRequest request)
    {
        // TODO(WS-7): map to an OutboundEventRequest of kind ReferralUpdate with status accepted/rejected and send.
        return StubResults.NotImplemented("ClinicianActionEndpoints.DecideReferral");
    }

    /// <summary>
    /// Will send an outcome <c>Procedure</c> or follow-up <c>DiagnosticReport</c>.
    /// </summary>
    /// <param name="request">The case, the recording clinician and the placeholder evidence kind.</param>
    /// <returns>501 until implemented; then 200 with the send result, 400 for an invalid request or 404 for an unknown case.</returns>
    private static ProblemHttpResult RecordOutcome(RecordOutcomeActionRequest request)
    {
        // TODO(WS-7): map to an OutboundEventRequest of kind Outcome and send.
        return StubResults.NotImplemented("ClinicianActionEndpoints.RecordOutcome");
    }

    /// <summary>
    /// Will send the completed case <c>Task</c> plus <c>Provenance</c> naming the authorizer.
    /// </summary>
    /// <param name="request">The case, the authorizer, the closure reason and any evidence or exception note.</param>
    /// <returns>
    /// 501 until implemented; then 200 with the send result (whose <c>accepted</c> flag is false if CareLoop refused
    /// the closure), 400 for an invalid request or 404 for an unknown case.
    /// </returns>
    private static ProblemHttpResult AuthorizeClosure(AuthorizeClosureActionRequest request)
    {
        // TODO(WS-7): map to an OutboundEventRequest of kind Closure and send; surface CareLoop's refusal in the result.
        return StubResults.NotImplemented("ClinicianActionEndpoints.AuthorizeClosure");
    }
}

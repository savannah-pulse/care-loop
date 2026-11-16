using System.Text.Json;
using HisSimulator.State;
using HisSimulator.Time;

namespace HisSimulator.Outbound;

/// <summary>Manual HIS actions follow the same FHIR path as scripted events; decisions are supplied by a person.</summary>
public static class ClinicianActionEndpoints
{
    /// <summary>Maps local case reads and the human-driven source actions.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapClinicianActionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/cases", (SimulatorStore store, CancellationToken ct) =>
        {
            ct.ThrowIfCancellationRequested();
            var summaries = store.GetInboxItems().Where(i => i.Kind == State.StaffInboxItemKind.HandoffSummary && i.Content is not null).ToArray();
            return TypedResults.Ok(new SimulatedCaseListResponse(store.GetCases().Select(c => SimulatedCaseItem.From(c,
                c.CareLoopTaskId is null ? null : summaries.FirstOrDefault(i => i.CareLoopTaskId == c.CareLoopTaskId)?.Content)).ToArray()));
        }).WithName("ListSimulatedCases").Produces<SimulatedCaseListResponse>();
        var actions = app.MapGroup("/actions").WithTags("Source actions");
        actions.MapPost("/clinical-review", async (ClinicalReviewActionRequest request, OutboundEventSender sender, ISimulatorClock clock, CancellationToken ct) =>
            TypedResults.Ok(await sender.SendAsync(Event(HisEventKind.ClinicalReview, request.CaseRef, request.Performer, "completed", clock,
                new() { ["urgency"] = request.Urgency }), ct))).WithName("RecordClinicalReview").ProducesProblem(400).ProducesProblem(404);
        actions.MapPost("/referral-decision", async (ReferralDecisionActionRequest request, OutboundEventSender sender, ISimulatorClock clock, CancellationToken ct) =>
            TypedResults.Ok(await sender.SendAsync(Event(HisEventKind.ReferralUpdate, request.CaseRef, request.Performer,
                (request.Accepted ?? throw new ArgumentException("The accepted field is required: state whether the referral is accepted or rejected."))
                    ? "accepted" : "rejected", clock, new() { ["reason"] = request.Reason }), ct)))
            .WithName("DecideReferral").ProducesProblem(400).ProducesProblem(404);
        actions.MapPost("/outcome", async (RecordOutcomeActionRequest request, OutboundEventSender sender, ISimulatorClock clock, CancellationToken ct) =>
            TypedResults.Ok(await sender.SendAsync(Event(HisEventKind.Outcome, request.CaseRef, request.Performer, "completed", clock,
                new() { ["evidenceKind"] = request.EvidenceKind, ["asFollowUpReport"] = request.AsFollowUpReport }), ct))).WithName("RecordOutcome");
        actions.MapPost("/closure", async (AuthorizeClosureActionRequest request, OutboundEventSender sender, ISimulatorClock clock, CancellationToken ct) =>
            TypedResults.Ok(await sender.SendAsync(Event(HisEventKind.Closure, request.CaseRef, request.Performer, "completed", clock,
                new()
                {
                    ["closureReason"] = request.ClosureReason, ["evidenceResourceKey"] = request.EvidenceResourceKey,
                    ["exceptionNote"] = request.ExceptionNote, ["nextSurveillanceDate"] = request.NextSurveillanceDate,
                    ["documentedAttemptReferences"] = request.DocumentedAttemptReferences,
                }), ct))).WithName("AuthorizeClosure").ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        MapTrack(actions, "/consultation-request", HisEventKind.ConsultationUpdate, true);
        MapTrack(actions, "/consultation-answer", HisEventKind.ConsultationUpdate);
        MapTrack(actions, "/referral", HisEventKind.ReferralUpdate);
        MapTrack(actions, "/appointment", HisEventKind.AppointmentUpdate);
        MapTrack(actions, "/consent", HisEventKind.ConsentRecorded);
        return app;
    }

    private static void MapTrack(RouteGroupBuilder group, string path, HisEventKind kind, bool requestOnly = false)
    {
        group.MapPost(path, async (TrackActionRequest request, OutboundEventSender sender, ISimulatorClock clock, CancellationToken ct) =>
        {
            if (requestOnly && request.Status is not (null or "requested"))
                throw new ArgumentException("A consultation request must have requested status.");
            if (!requestOnly && kind == HisEventKind.ConsultationUpdate && request.Status is not ("accepted" or "rejected" or "completed"))
                throw new ArgumentException("A consultation answer must be accepted, rejected or completed.");
            return TypedResults.Ok(await sender.SendAsync(Event(kind, request.CaseRef, request.Performer,
                requestOnly ? "requested" : request.Status, clock, new()
                {
                    ["proposedOwner"] = request.ProposedOwner, ["reason"] = request.Reason,
                    ["receivingFacility"] = request.ReceivingFacility, ["location"] = request.Location,
                    ["scheduledFor"] = request.ScheduledFor, ["caregiverContact"] = request.CaregiverContact,
                }), ct));
        }).WithName(path[1..]).WithSummary("Sends a source-authored " + kind + " event through FHIR only.")
            .ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).ProducesProblem(502);
    }

    private static OutboundEventRequest Event(HisEventKind kind, string caseRef, ActingStaff? staff, string? status,
        ISimulatorClock clock, Dictionary<string, object?> values)
    {
        if (kind is not HisEventKind.AppointmentUpdate) FhirSupport.Staff(staff);
        return new OutboundEventRequest(kind, caseRef, Guid.NewGuid().ToString("N"), clock.UtcNow, status, staff, null, null,
            values.Where(pair => pair.Value is not null).ToDictionary(pair => pair.Key,
                pair => JsonSerializer.SerializeToElement(pair.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
    }
}

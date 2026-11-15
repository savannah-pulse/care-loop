using HisSimulator.State;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace HisSimulator.Outbound;

/// <summary>
/// Turns one <see cref="OutboundEventRequest"/> into FHIR resources and sends them to CareLoop.
/// </summary>
/// <remarks>
/// This is the single outbound path shared by the scenario runner and the manual clinician actions,
/// so scripted and hand-triggered events are built identically. It picks the builder for the
/// <see cref="HisEventKind"/>, sends the resources in dependency order (for example <c>ServiceRequest</c>
/// before its <c>Task</c>). Closure travels as one completed Task containing its matching human
/// Provenance, because CareLoop requires the authorizer in the same inbound resource. It updates the
/// <see cref="SimulatedCase"/> with the ids it sent.
/// </remarks>
public sealed class OutboundEventSender
{
    private readonly CareLoopFhirClient _client;
    private readonly SimulatorStore _store;
    private readonly ScreeningResultBuilder _screeningResults;
    private readonly ClinicalReviewTaskBuilder _clinicalReviews;
    private readonly ConsultationBuilder _consultations;
    private readonly ReferralBuilder _referrals;
    private readonly AppointmentBuilder _appointments;
    private readonly OutcomeBuilder _outcomes;
    private readonly ClosureBuilder _closures;
    private readonly ConsentBuilder _consents;

    /// <summary>
    /// Creates the sender.
    /// </summary>
    /// <param name="client">Sends resources to CareLoop and logs each exchange.</param>
    /// <param name="store">Holds the simulated cases the events refer to.</param>
    /// <param name="screeningResults">Builds screening results.</param>
    /// <param name="clinicalReviews">Builds clinical review tasks.</param>
    /// <param name="consultations">Builds consultation requests and tasks.</param>
    /// <param name="referrals">Builds referral requests and tasks.</param>
    /// <param name="appointments">Builds appointments.</param>
    /// <param name="outcomes">Builds outcome resources.</param>
    /// <param name="closures">Builds closure tasks and provenance.</param>
    /// <param name="consents">Builds caregiver consent grants and withdrawals.</param>
    public OutboundEventSender(
        CareLoopFhirClient client,
        SimulatorStore store,
        ScreeningResultBuilder screeningResults,
        ClinicalReviewTaskBuilder clinicalReviews,
        ConsultationBuilder consultations,
        ReferralBuilder referrals,
        AppointmentBuilder appointments,
        OutcomeBuilder outcomes,
        ClosureBuilder closures,
        ConsentBuilder consents)
    {
        _client = client;
        _store = store;
        _screeningResults = screeningResults;
        _clinicalReviews = clinicalReviews;
        _consultations = consultations;
        _referrals = referrals;
        _appointments = appointments;
        _outcomes = outcomes;
        _closures = closures;
        _consents = consents;
    }

    /// <summary>
    /// Builds and sends the FHIR resources for one HIS event.
    /// </summary>
    /// <param name="request">The event to send.</param>
    /// <param name="cancellationToken">Cancels the HTTP calls.</param>
    /// <returns>
    /// One <see cref="FhirSendResult"/> per resource sent. A rejection by CareLoop is reported in the result
    /// (see <see cref="OutboundEventResult.Accepted"/>), not thrown.
    /// </returns>
    /// <exception cref="KeyNotFoundException">
    /// The event is not a screening result and no <see cref="SimulatedCase"/> exists for <see cref="OutboundEventRequest.CaseRef"/>.
    /// </exception>
    /// <exception cref="ArgumentException">The request lacks a value its builder requires.</exception>
    /// <exception cref="HttpRequestException">CareLoop could not be reached.</exception>
    public async Task<OutboundEventResult> SendAsync(OutboundEventRequest request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CaseRef);
        var gate = _store.GateFor(request.CaseRef);
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (request.Kind == HisEventKind.ScreeningResult)
            {
                var report = _screeningResults.Build(request);
                var sent = await _client.CreateAsync(report, request.CaseRef, cancellationToken);
                if (sent.Accepted) _store.UpsertCase(_screeningResults.CreateCase(request, report));
                return new OutboundEventResult(request.Kind, request.CaseRef, request.SourceEventKey, [sent]);
            }
            var @case = _store.GetCase(request.CaseRef) ?? throw new KeyNotFoundException("The simulator has not sent a result for this case.");
            var resources = new List<(Resource Resource, string Key)>();
            switch (request.Kind)
            {
                case HisEventKind.ClinicalReview: resources.Add((_clinicalReviews.Build(@case, request), "review-task")); break;
                case HisEventKind.ConsultationUpdate:
                    if (!@case.ResourceIds.ContainsKey("consultation-service-request"))
                        resources.Add((_consultations.BuildServiceRequest(@case, request), "consultation-service-request"));
                    resources.Add((_consultations.BuildTask(@case, request), "consultation-task")); break;
                case HisEventKind.ReferralUpdate:
                    if (!@case.ResourceIds.ContainsKey("referral-service-request"))
                        resources.Add((_referrals.BuildServiceRequest(@case, request), "referral-service-request"));
                    resources.Add((_referrals.BuildTask(@case, request), "referral-task")); break;
                case HisEventKind.AppointmentUpdate: resources.Add((_appointments.Build(@case, request), "appointment")); break;
                case HisEventKind.Outcome:
                    var asReport = false;
                    if (request.Params.TryGetValue("asFollowUpReport", out var value))
                    {
                        if (value.ValueKind is not (System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False))
                            throw new ArgumentException("asFollowUpReport must be a boolean.");
                        asReport = value.GetBoolean();
                    }
                    resources.Add((asReport ? _outcomes.BuildFollowUpReport(@case, request) : _outcomes.BuildProcedure(@case, request), "outcome"));
                    break;
                case HisEventKind.Closure:
                    var closure = _closures.BuildTask(@case, request);
                    var provenance = _closures.BuildProvenance(@case, request, closure);
                    provenance.Target = [new ResourceReference("#")];
                    provenance.Meta = new Meta { Tag = provenance.Meta?.Tag ?? [] };
                    closure.Contained.Add(provenance);
                    closure.RelevantHistory.Add(new ResourceReference("#" + provenance.Id));
                    resources.Add((closure, "closure-task")); break;
                case HisEventKind.ConsentRecorded: resources.Add((_consents.Build(@case, request), "consent")); break;
                default: throw new ArgumentException("This HIS event kind is unsupported.");
            }
            var results = new List<FhirSendResult>();
            foreach (var (resource, key) in resources)
            {
                var update = key == "closure-task" || @case.ResourceIds.ContainsKey(key) && key != "outcome";
                var result = update
                    ? await _client.UpdateAsync(resource, request.CaseRef, cancellationToken)
                    : await _client.CreateAsync(resource, request.CaseRef, cancellationToken);
                results.Add(result);
                if (!result.Accepted) break;
                var ids = new Dictionary<string, string>(@case.ResourceIds) { [key] = resource.Id! };
                if (key == "outcome")
                {
                    var reference = resource.TypeName + "/" + resource.Id;
                    ids[key] = reference;
                    ids[request.SourceEventKey] = reference;
                }
                @case = @case with { ResourceIds = ids };
                _store.UpsertCase(@case);
            }
            return new OutboundEventResult(request.Kind, request.CaseRef, request.SourceEventKey, results);
        }
        finally { gate.Release(); }
    }
}

using HisSimulator.State;

namespace HisSimulator.Outbound;

/// <summary>
/// Turns one <see cref="OutboundEventRequest"/> into FHIR resources and sends them to CareLoop.
/// </summary>
/// <remarks>
/// This is the single outbound path shared by the scenario runner and the manual clinician actions,
/// so scripted and hand-triggered events are built identically. It picks the builder for the
/// <see cref="HisEventKind"/>, sends the resources in dependency order (for example <c>ServiceRequest</c>
/// before its <c>Task</c>, case <c>Task</c> before its <c>Provenance</c>) and updates the
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
    public OutboundEventSender(
        CareLoopFhirClient client,
        SimulatorStore store,
        ScreeningResultBuilder screeningResults,
        ClinicalReviewTaskBuilder clinicalReviews,
        ConsultationBuilder consultations,
        ReferralBuilder referrals,
        AppointmentBuilder appointments,
        OutcomeBuilder outcomes,
        ClosureBuilder closures)
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
    public Task<OutboundEventResult> SendAsync(OutboundEventRequest request, CancellationToken cancellationToken)
    {
        // TODO(WS-7): switch on request.Kind, build with the matching builder, send via CareLoopFhirClient
        // (POST for a first state, PUT for a later state of the same resource), upsert the SimulatedCase.
        throw new NotImplementedException("WS7: OutboundEventSender.SendAsync");
    }
}

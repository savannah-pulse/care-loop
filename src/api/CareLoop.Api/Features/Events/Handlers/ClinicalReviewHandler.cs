using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Time;

namespace CareLoop.Api.Features.Events.Handlers;

/// <summary>
/// Records the clinician&apos;s review and starts follow-up.
/// </summary>
/// <remarks>
/// Will check <see cref="CareLoop.Domain.Rules.TransitionRules"/>, store the reviewed urgency exactly as received (CareLoop never sets or changes urgency), move the case to follow-up active, request the patient notification and set the next action and deadline for that urgency. An urgency code the pathway does not know is refused.
/// </remarks>
public sealed class ClinicalReviewHandler : IEventHandler
{
    private readonly IPathwayProvider _pathways;
    private readonly IClock _clock;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="pathways">Supplies the pathway definition the rules are evaluated against.</param>
    /// <param name="clock">Supplies the current time for deadlines.</param>
    public ClinicalReviewHandler(IPathwayProvider pathways, IClock clock)
    {
        _pathways = pathways;
        _clock = clock;
    }

    /// <inheritdoc/>
    public CaseEventType EventType => CaseEventType.ClinicalReviewCompleted;

    /// <inheritdoc/>
    /// <remarks>Expects the envelope's payload to be a <see cref="ClinicalReviewPayload"/>.</remarks>
    public Task<EventHandlingResult> HandleAsync(Case? existing, EventEnvelope envelope, CancellationToken ct)
    {
        // TODO(WS-2): apply the review via TransitionRules, Case.RecordReview, Case.BeginNotification and DeadlinePolicy.
        throw new NotImplementedException("WS2: ClinicalReviewHandler.HandleAsync");
    }
}

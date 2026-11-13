using CareLoop.Api.Features.Outbox;
using CareLoop.Domain.Events.DomainEvents;

namespace CareLoop.Api.Features.Summaries;

/// <summary>
/// Outbox subscriber that drafts a handoff summary when ownership is proposed (decision 11).
/// </summary>
/// <remarks>
/// The receiving clinician finds a draft already waiting instead of having to ask for one. It is
/// still only a draft: it is stored in status <c>Draft</c>, it is never published, and the FHIR
/// publisher sends a <c>DocumentReference</c> only once a person has approved it.
/// <para>
/// Drafting costs a model call and sends facts out of CareLoop with nobody having asked, so this
/// subscriber runs only when <c>Ai:AutoDraftOnHandoff</c> is <see langword="true"/>, and it draws
/// a draft once per case: a second <see cref="HandoffProposed"/> for a case that already has an
/// unapproved draft does nothing. Being an outbox subscriber, a retried delivery must not produce
/// a second draft either, which the same check covers.
/// </para>
/// <para>
/// A failure here never fails the handoff. The proposal has already happened; the summary is a
/// convenience on top of it, so an unavailable provider leaves the case with no draft and an
/// audited reason, and the clinician can ask for one on the case screen.
/// </para>
/// </remarks>
public sealed class AutoDraftOnHandoff : IDomainEventSubscriber
{
    private readonly HandoffSummaryDrafter _drafter;

    /// <summary>
    /// Creates the subscriber.
    /// </summary>
    /// <param name="drafter">Produces and stores the draft.</param>
    public AutoDraftOnHandoff(HandoffSummaryDrafter drafter)
    {
        _drafter = drafter;
    }

    /// <inheritdoc/>
    public string Name => "AutoDraftOnHandoff";

    /// <inheritdoc/>
    public Task HandleAsync(IDomainEvent domainEvent, CancellationToken ct)
    {
        // TODO(WS-9): ignore everything but HandoffProposed; skip when the case already has a Draft
        // or Approved summary; otherwise draft with trigger HandoffProposed. Swallow
        // AiDraftUnavailableException after auditing it so the delivery is not retried forever.
        throw new NotImplementedException("WS9: AutoDraftOnHandoff.HandleAsync");
    }
}

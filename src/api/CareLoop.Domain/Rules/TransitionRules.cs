using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Pathways;

namespace CareLoop.Domain.Rules;

/// <summary>
/// Guards every stage transition of a case.
/// </summary>
/// <remarks>
/// A case may only move when the right kind of event arrives from someone allowed to perform it.
/// Guarding transitions centrally prevents a case from skipping review, from leaving follow-up
/// without an outcome, or from ever leaving <see cref="CaseStage.Closed"/>. The entry and exit
/// rules themselves come from the pathway definition so they can change without code edits.
/// </remarks>
public static class TransitionRules
{
    /// <summary>
    /// The events that report progress on a parallel track rather than moving the case between stages.
    /// </summary>
    private static readonly CaseEventType[] TrackEvents =
    [
        CaseEventType.NotificationUpdated,
        CaseEventType.ConsultationUpdated,
        CaseEventType.ReferralUpdated,
        CaseEventType.AppointmentUpdated,
    ];

    /// <summary>
    /// The stages during which track events are accepted without changing the stage.
    /// </summary>
    /// <remarks>
    /// Follow-up is where the tracks run. Closure review is included because the hospital system
    /// may report an attendance or acknowledgement after the outcome that moved the case there,
    /// and refusing that report would leave a required track incomplete through no fault of anyone.
    /// </remarks>
    private static readonly CaseStage[] StagesAcceptingTrackEvents =
    [
        CaseStage.FollowUpActive,
        CaseStage.ClosureReview,
    ];

    /// <summary>
    /// Decides whether an event may be applied to a case and which stage results.
    /// </summary>
    /// <param name="case">The case in its current state.</param>
    /// <param name="eventType">The kind of event that arrived.</param>
    /// <param name="actor">Who performed the action at the source, or <see langword="null"/> when the source did not say.</param>
    /// <param name="pathway">The pathway definition supplying the stage rules and allowed roles.</param>
    /// <returns>Whether the event is allowed, the resulting stage and the next action to set.</returns>
    /// <remarks>
    /// <para>
    /// Refuses: any event on a closed case; an event whose type does not leave or belong to the
    /// current stage; an actor whose role is not allowed for the transition, or no actor at all when
    /// the stage names allowed roles. Track events (notification, consultation, referral,
    /// appointment) are allowed during follow-up and closure review without changing the stage. A
    /// further <see cref="CaseEventType.OutcomeRecorded"/> during closure review is allowed without
    /// changing the stage, by the roles the pathway lets record an outcome, because the design
    /// records diagnosis, treatment plan and treatment started as separate outcomes.
    /// CareLoop's own internal events (<see cref="CaseEventType.DeadlineElapsed"/>,
    /// <see cref="CaseEventType.EscalationAcknowledged"/>) and a consent record
    /// (<see cref="CaseEventType.ConsentRecorded"/>) are allowed on any open case without
    /// changing the stage, because they are coordination, not pathway progress.
    /// </para>
    /// <para>This method never mutates the case.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="case"/> or <paramref name="pathway"/> is <see langword="null"/>.</exception>
    public static TransitionResult Evaluate(Case @case, CaseEventType eventType, Actor? actor, PathwayDefinition pathway)
    {
        ArgumentNullException.ThrowIfNull(@case);
        ArgumentNullException.ThrowIfNull(pathway);

        if (@case.IsClosed)
        {
            return Refuse($"Case is closed; a closed case accepts no further events ({eventType} refused).");
        }

        var current = pathway.Stages.FirstOrDefault(s => s.Stage == @case.Stage);
        if (current is null)
        {
            return Refuse($"Pathway '{pathway.Id}' defines no rule for stage {@case.Stage}.");
        }

        if (current.LeavesOn.Contains(eventType))
        {
            if (current.AllowedRoles.Count > 0)
            {
                if (actor is null)
                {
                    return Refuse($"{eventType} must name who performed it; stage {@case.Stage} allows roles {Join(current.AllowedRoles)}.");
                }

                if (!current.AllowedRoles.Contains(actor.Role, StringComparer.OrdinalIgnoreCase))
                {
                    return Refuse($"Role '{actor.Role}' may not perform {eventType} in stage {@case.Stage}; allowed roles are {Join(current.AllowedRoles)}.");
                }
            }

            if (current.Next is null)
            {
                return Refuse($"Pathway '{pathway.Id}' names no next stage after {@case.Stage} for {eventType}.");
            }

            var next = pathway.Stages.FirstOrDefault(s => s.Stage == current.Next.Value);

            return new TransitionResult
            {
                Verdict = RuleResult.Allow(),
                NextStage = current.Next,
                NextAction = next?.NextActionOnEntry,
            };
        }

        if (TrackEvents.Contains(eventType) && StagesAcceptingTrackEvents.Contains(@case.Stage))
        {
            return new TransitionResult { Verdict = RuleResult.Allow() };
        }

        if (eventType == CaseEventType.OutcomeRecorded && @case.Stage == CaseStage.ClosureReview)
        {
            // A further outcome (a treatment plan after a diagnosis, treatment after the plan) adds
            // evidence without leaving closure review. Who may record it is the rule of the stage
            // that an outcome leaves, so the same roles apply wherever the outcome arrives.
            var outcomeStage = pathway.Stages.FirstOrDefault(s => s.LeavesOn.Contains(CaseEventType.OutcomeRecorded));
            if (outcomeStage is { AllowedRoles.Count: > 0 })
            {
                if (actor is null)
                {
                    return Refuse($"{eventType} must name who performed it; allowed roles are {Join(outcomeStage.AllowedRoles)}.");
                }

                if (!outcomeStage.AllowedRoles.Contains(actor.Role, StringComparer.OrdinalIgnoreCase))
                {
                    return Refuse($"Role '{actor.Role}' may not record an outcome; allowed roles are {Join(outcomeStage.AllowedRoles)}.");
                }
            }

            return new TransitionResult { Verdict = RuleResult.Allow() };
        }

        if (eventType is CaseEventType.DeadlineElapsed or CaseEventType.EscalationAcknowledged or CaseEventType.ConsentRecorded)
        {
            return new TransitionResult { Verdict = RuleResult.Allow() };
        }

        return Refuse($"{eventType} neither leaves nor belongs to stage {@case.Stage}; the stage leaves on {Join(current.LeavesOn.Select(e => e.ToString()).ToList())}.");
    }

    private static TransitionResult Refuse(string reason) =>
        new() { Verdict = RuleResult.Deny(reason) };

    private static string Join(IReadOnlyList<string> values) =>
        values.Count == 0 ? "(none)" : string.Join(", ", values);
}

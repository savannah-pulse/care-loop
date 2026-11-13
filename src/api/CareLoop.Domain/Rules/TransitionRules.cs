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
    /// Decides whether an event may be applied to a case and which stage results.
    /// </summary>
    /// <param name="case">The case in its current state.</param>
    /// <param name="eventType">The kind of event that arrived.</param>
    /// <param name="actor">Who performed the action at the source, or <see langword="null"/> when the source did not say.</param>
    /// <param name="pathway">The pathway definition supplying the stage rules and allowed roles.</param>
    /// <returns>Whether the event is allowed, the resulting stage and the next action to set.</returns>
    /// <remarks>
    /// Must refuse: any event on a closed case; an event whose type does not leave or belong to the
    /// current stage; an actor whose role is not allowed for the transition. Track events
    /// (notification, consultation, referral, appointment) are allowed during follow-up without
    /// changing the stage. This method never mutates the case.
    /// </remarks>
    public static TransitionResult Evaluate(Case @case, CaseEventType eventType, Actor? actor, PathwayDefinition pathway)
    {
        // TODO(WS-1): look up the stage definition, check LeavesOn and AllowedRoles, return the verdict.
        throw new NotImplementedException("WS1: TransitionRules.Evaluate");
    }
}

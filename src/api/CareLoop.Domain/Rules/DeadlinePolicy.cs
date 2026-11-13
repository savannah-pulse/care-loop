using CareLoop.Domain.Cases;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Time;

namespace CareLoop.Domain.Rules;

/// <summary>
/// Computes deadlines and escalation steps from the pathway definition.
/// </summary>
/// <remarks>
/// A next action without a deadline is a case that can silently stall. This policy gives every
/// next action a due time and decides when to warn and when to escalate. It contains no
/// durations of its own: every value comes from the pathway definition, because clinical
/// deadlines are configuration that a clinical adviser must approve. Notifying someone never
/// stops or resets a clock.
/// </remarks>
public static class DeadlinePolicy
{
    /// <summary>
    /// Computes when a next action is due.
    /// </summary>
    /// <param name="nextAction">Key of the next action being set.</param>
    /// <param name="urgencyCode">The clinician-reviewed urgency of the case, or <see langword="null"/> before review.</param>
    /// <param name="pathway">The pathway definition supplying the durations.</param>
    /// <param name="clock">Supplies the time the next action is set.</param>
    /// <returns>The due time: now plus the matching duration, preferring an urgency-specific deadline over the general one.</returns>
    /// <exception cref="KeyNotFoundException">The pathway defines no deadline for <paramref name="nextAction"/>.</exception>
    public static DateTimeOffset ComputeDueAt(string nextAction, string? urgencyCode, PathwayDefinition pathway, IClock clock)
    {
        // TODO(WS-5): find the DeadlineDefinition (urgency-specific first) and add its Duration to clock.UtcNow.
        throw new NotImplementedException("WS5: DeadlinePolicy.ComputeDueAt");
    }

    /// <summary>
    /// Classifies a case's next action against its deadline.
    /// </summary>
    /// <param name="case">The case to check.</param>
    /// <param name="pathway">The pathway definition supplying the warning window.</param>
    /// <param name="clock">Supplies the current time.</param>
    /// <returns>Whether the case is on track, approaching its deadline or overdue.</returns>
    public static DeadlineStatus Classify(Case @case, PathwayDefinition pathway, IClock clock)
    {
        // TODO(WS-5): compare clock.UtcNow with Case.DueAt and the deadline's WarnBefore window.
        throw new NotImplementedException("WS5: DeadlinePolicy.Classify");
    }

    /// <summary>
    /// Finds the next escalation step an overdue case has reached but not yet been escalated to.
    /// </summary>
    /// <param name="case">The overdue case, with the escalations already raised.</param>
    /// <param name="pathway">The pathway definition supplying the escalation ladder.</param>
    /// <param name="clock">Supplies the current time.</param>
    /// <returns>The step to escalate to now, or <see langword="null"/> when no further step is due.</returns>
    /// <remarks>Each ladder step is raised at most once per overdue deadline, so a case is not escalated repeatedly to the same role.</remarks>
    public static EscalationStep? NextEscalationStep(Case @case, PathwayDefinition pathway, IClock clock)
    {
        // TODO(WS-5): compare time overdue with EscalationStep.AfterOverdue and the levels already raised.
        throw new NotImplementedException("WS5: DeadlinePolicy.NextEscalationStep");
    }
}

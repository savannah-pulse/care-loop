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
    /// Key of the next action that waits for the patient to acknowledge the notification.
    /// </summary>
    /// <remarks>
    /// An overdue case on this action is also flagged as unacknowledged. The key is named here, once, and
    /// a test fails if a pathway file does not define a deadline for it, so a renamed action can never
    /// silently stop the flag.
    /// </remarks>
    public const string AcknowledgeNotificationAction = "acknowledge-notification";

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
        ArgumentNullException.ThrowIfNull(clock);
        return clock.UtcNow.Add(FindDeadline(nextAction, urgencyCode, pathway).Duration);
    }

    /// <summary>Finds the configured deadline, preferring the clinician-reviewed urgency's entry.</summary>
    /// <param name="nextAction">The next-action key.</param>
    /// <param name="urgencyCode">The reviewed urgency, or <see langword="null"/> before review.</param>
    /// <param name="pathway">The pathway supplying the deadlines.</param>
    /// <returns>The urgency-specific deadline, or the general deadline when no specific entry matches.</returns>
    /// <remarks>All callers use the same lookup so intake and deadline monitoring cannot disagree about the clock. Keys are compared ignoring case.</remarks>
    /// <exception cref="ArgumentException"><paramref name="nextAction"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="pathway"/> is <see langword="null"/>.</exception>
    /// <exception cref="KeyNotFoundException">No applicable deadline is configured.</exception>
    public static DeadlineDefinition FindDeadline(string nextAction, string? urgencyCode, PathwayDefinition pathway)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nextAction);
        ArgumentNullException.ThrowIfNull(pathway);

        var candidates = pathway.Deadlines
            .Where(d => string.Equals(d.NextAction, nextAction, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return candidates.FirstOrDefault(d => urgencyCode is not null
                && string.Equals(d.UrgencyCode, urgencyCode, StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault(d => d.UrgencyCode is null)
            ?? throw new KeyNotFoundException(
                $"Pathway '{pathway.Id}' defines no deadline for next action '{nextAction}'.");
    }

    /// <summary>
    /// Classifies a case's next action against its deadline.
    /// </summary>
    /// <param name="case">The case to check.</param>
    /// <param name="pathway">The pathway definition supplying the warning window.</param>
    /// <param name="clock">Supplies the current time.</param>
    /// <returns>Whether the case is on track, approaching its deadline or overdue.</returns>
    /// <remarks>A deadline is overdue only after its due instant; at that instant the action can still be completed on time.</remarks>
    /// <exception cref="KeyNotFoundException">The next action has no applicable deadline.</exception>
    public static DeadlineStatus Classify(Case @case, PathwayDefinition pathway, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(@case);
        ArgumentNullException.ThrowIfNull(pathway);
        ArgumentNullException.ThrowIfNull(clock);
        if (@case.IsClosed || @case.DueAt is not { } dueAt)
        {
            return DeadlineStatus.OnTrack;
        }

        var deadline = FindDeadline(@case.NextAction!, @case.Urgency, pathway);
        var now = clock.UtcNow;
        return now > dueAt
            ? DeadlineStatus.Overdue
            : dueAt - now <= deadline.WarnBefore ? DeadlineStatus.Approaching : DeadlineStatus.OnTrack;
    }

    /// <summary>
    /// Finds the next escalation step an overdue case has reached but not yet been escalated to.
    /// </summary>
    /// <param name="case">The overdue case, with the escalations already raised.</param>
    /// <param name="pathway">The pathway definition supplying the escalation ladder.</param>
    /// <param name="clock">Supplies the current time.</param>
    /// <returns>The step to escalate to now, or <see langword="null"/> when no further step is due.</returns>
    /// <remarks>
    /// Each ladder step is raised at most once per overdue deadline, so a case is not escalated
    /// repeatedly to the same role. Resolved escalations and ones raised before the current due time
    /// belong to earlier clocks. Acknowledgement is not resolution and never suppresses the next step.
    /// </remarks>
    public static EscalationStep? NextEscalationStep(Case @case, PathwayDefinition pathway, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(@case);
        ArgumentNullException.ThrowIfNull(pathway);
        ArgumentNullException.ThrowIfNull(clock);
        var now = clock.UtcNow;
        if (@case.IsClosed || @case.DueAt is not { } dueAt || now <= dueAt)
        {
            return null;
        }

        var overdue = now - dueAt;
        return pathway.EscalationLadder.OrderBy(s => s.Level)
            .FirstOrDefault(s => overdue >= s.AfterOverdue
                && !@case.Escalations.Any(e => e.IsOpen && e.RaisedAt >= dueAt && e.Level == s.Level));
    }
}

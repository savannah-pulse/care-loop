using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Rules;
using CareLoop.Domain.Time;
using CareLoop.Domain.Tracks;

namespace CareLoop.Api.Features.Events.Handlers;

/// <summary>
/// The small set of steps every event handler shares: reading the typed payload, requiring a
/// case, building results, and setting a next action with its deadline from the pathway.
/// </summary>
/// <remarks>
/// Handlers only ask the Domain rules and call methods on <see cref="Case"/>. Keeping the shared
/// plumbing here keeps each handler to the one decision it makes.
/// </remarks>
internal static class HandlerSupport
{
    /// <summary>
    /// Key of the next action an owner holds while follow-up care is being arranged: after the
    /// patient acknowledged the notification, or after a missed appointment was rebooked.
    /// </summary>
    /// <remarks>
    /// A case may never be without a next action and a deadline, and the pathway's stage rules name
    /// no action for this part of follow-up, so this key names it. How long it may take is still
    /// configuration: the pathway must define a deadline for it.
    /// </remarks>
    public const string ArrangeFollowUpAction = "arrange-follow-up";

    /// <summary>The role key CareLoop's own automation acts in when a source names no actor.</summary>
    public const string SystemRole = "system";

    /// <summary>
    /// Reads the payload as the type the handler expects.
    /// </summary>
    /// <typeparam name="TPayload">The payload type matching the handler's event type.</typeparam>
    /// <param name="envelope">The event.</param>
    /// <returns>The typed payload.</returns>
    /// <exception cref="InvalidOperationException">The payload is of another type.</exception>
    public static TPayload Payload<TPayload>(EventEnvelope envelope)
        where TPayload : EventPayload
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return envelope.Payload as TPayload
            ?? throw new InvalidOperationException(
                $"Event {envelope.Type} expects a {typeof(TPayload).Name} payload but received {envelope.Payload.GetType().Name}.");
    }

    /// <summary>
    /// Requires that the event belongs to an existing case.
    /// </summary>
    /// <param name="existing">The case found for the event, or <see langword="null"/>.</param>
    /// <param name="eventType">The event type, for the error.</param>
    /// <returns>The case.</returns>
    /// <exception cref="InvalidOperationException">There is no case.</exception>
    public static Case RequireCase(Case? existing, CaseEventType eventType) =>
        existing ?? throw new InvalidOperationException($"Event {eventType} needs an existing case; only a screening result may open one.");

    /// <summary>
    /// The actor named by the source, or the source system acting as <see cref="SystemRole"/> when none was named.
    /// </summary>
    /// <param name="envelope">The event.</param>
    /// <returns>An actor that is never <see langword="null"/>.</returns>
    public static Actor ActorOrSource(EventEnvelope envelope) =>
        envelope.Actor ?? new Actor { Reference = envelope.SourceSystem, Role = SystemRole };

    /// <summary>
    /// Builds the result for an event that changed the case.
    /// </summary>
    /// <param name="case">The changed case.</param>
    /// <param name="evidenceReference">The evidence the event carried, if any.</param>
    /// <returns>An applied result.</returns>
    public static Task<EventHandlingResult> Applied(Case @case, string? evidenceReference = null) =>
        Task.FromResult(new EventHandlingResult { Case = @case, Applied = true, EvidenceReference = evidenceReference });

    /// <summary>
    /// Builds the result for an event that changed the case, with a plain-words summary for the audit trail.
    /// </summary>
    /// <param name="case">The changed case.</param>
    /// <param name="evidenceReference">The evidence the event carried, if any.</param>
    /// <param name="summary">What the event recorded; never patient detail.</param>
    /// <returns>An applied result.</returns>
    public static Task<EventHandlingResult> Applied(Case @case, string? evidenceReference, string summary) =>
        Task.FromResult(new EventHandlingResult { Case = @case, Applied = true, EvidenceReference = evidenceReference, Summary = summary });

    /// <summary>
    /// Builds the result for an event a rule refused. The case is unchanged and the reason is audited.
    /// </summary>
    /// <param name="case">The unchanged case.</param>
    /// <param name="reason">Why the event was refused.</param>
    /// <returns>A refused result.</returns>
    public static Task<EventHandlingResult> Refused(Case @case, string reason) =>
        Task.FromResult(new EventHandlingResult { Case = @case, Applied = false, RejectionReason = reason });

    /// <summary>
    /// Gives the case a next action and the deadline the pathway sets for it.
    /// </summary>
    /// <param name="case">The case.</param>
    /// <param name="nextAction">Key of the next action.</param>
    /// <param name="pathway">The pathway supplying the deadline.</param>
    /// <param name="clock">Supplies now, from which the deadline is counted.</param>
    /// <exception cref="KeyNotFoundException">The pathway defines no deadline for <paramref name="nextAction"/>.</exception>
    public static void SetNextAction(Case @case, string nextAction, PathwayDefinition pathway, IClock clock)
    {
        var dueAt = DeadlinePolicy.ComputeDueAt(nextAction, @case.Urgency, pathway, clock);
        @case.SetNextAction(nextAction, dueAt, clock.UtcNow);
    }

    /// <summary>
    /// Finds the deadline definition that applies to a next action, preferring an urgency-specific entry.
    /// </summary>
    /// <param name="nextAction">Key of the next action.</param>
    /// <param name="urgencyCode">The clinician-reviewed urgency of the case, or <see langword="null"/> before review.</param>
    /// <param name="pathway">The pathway definition supplying the deadlines.</param>
    /// <returns>The matching definition.</returns>
    /// <remarks>
    /// Delegates to the Domain policy so deadline validation and monitoring use identical rules.
    /// </remarks>
    /// <exception cref="KeyNotFoundException">The pathway defines no deadline for <paramref name="nextAction"/>.</exception>
    public static DeadlineDefinition FindDeadline(string nextAction, string? urgencyCode, PathwayDefinition pathway)
        => DeadlinePolicy.FindDeadline(nextAction, urgencyCode, pathway);

    /// <summary>
    /// Finds the next action the pathway prescribes when a track hits an exception.
    /// </summary>
    /// <param name="pathway">The pathway.</param>
    /// <param name="kind">The track that failed.</param>
    /// <returns>The next action key.</returns>
    /// <exception cref="KeyNotFoundException">The pathway does not configure the track.</exception>
    public static string ExceptionAction(PathwayDefinition pathway, TrackKind kind) =>
        pathway.Tracks.FirstOrDefault(t => t.Kind == kind)?.NextActionOnException
        ?? throw new KeyNotFoundException($"Pathway '{pathway.Id}' does not configure the {kind} track.");

    /// <summary>
    /// Checks that the pathway can give a deadline for an action before any change is made, so a
    /// missing deadline refuses the event instead of leaving a half-applied case.
    /// </summary>
    /// <param name="case">The case, for its urgency.</param>
    /// <param name="nextAction">Key of the next action.</param>
    /// <param name="pathway">The pathway.</param>
    /// <param name="reason">Why the action cannot be given a deadline, when it cannot.</param>
    /// <returns><see langword="true"/> when a deadline exists.</returns>
    public static bool HasDeadline(Case @case, string nextAction, PathwayDefinition pathway, out string? reason)
    {
        try
        {
            FindDeadline(nextAction, @case.Urgency, pathway);
            reason = null;
            return true;
        }
        catch (KeyNotFoundException ex)
        {
            reason = ex.Message;
            return false;
        }
    }
}

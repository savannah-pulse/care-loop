using PatientChannelSimulator.Outbound;
using PatientChannelSimulator.Scenarios;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Inbound;

/// <summary>
/// Makes a simulated patient reply to a delivered notification the way their behaviour says.
/// </summary>
/// <remarks>
/// <para>
/// This is what makes the simulator play the patient instead of following a script. After a
/// notification is delivered, the responder reads the patient's <see cref="PatientBehaviour"/>:
/// if this is the notification the patient responds to
/// (<see cref="PatientBehaviour.RespondToNotification"/>), it waits
/// <see cref="PatientBehaviour.RespondAfter"/> and then sends the reply through
/// <see cref="ReplySender"/>, exactly as typing it in the inbox would: 1 for "I got this message", 2 to
/// ask for a call.
/// </para>
/// <para>
/// It only ever reacts to a patient message that was received and delivered. With no notification
/// from CareLoop there is no response, so a scenario cannot appear to pass when CareLoop sent nothing.
/// A patient whose behaviour is <see cref="PatientResponseBehaviour.None"/> stays silent for good,
/// and a caregiver's or a staff member's message is never replied to.
/// </para>
/// </remarks>
public sealed class AutomaticResponder
{
    private readonly ReplySender _replies;
    private readonly SimulatorStore _store;
    private readonly ISimulatorClock _clock;

    /// <summary>
    /// Creates the responder.
    /// </summary>
    /// <param name="replies">Sends the reply to CareLoop and updates the inbox message.</param>
    /// <param name="store">Supplies the patient's behaviour and notification count, and stores the reaction and any delayed response.</param>
    /// <param name="clock">Source of the time the response is due and sent.</param>
    public AutomaticResponder(ReplySender replies, SimulatorStore store, ISimulatorClock clock)
    {
        _replies = replies;
        _store = store;
        _clock = clock;
    }

    /// <summary>
    /// Decides whether the patient responds to a delivered message and, if so, sends the response
    /// now or queues it for its due time.
    /// </summary>
    /// <param name="message">The inbox message, already marked delivered.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>
    /// A task that completes once the decision is recorded. It does not wait for a delayed
    /// response to be sent; that happens later, in <see cref="SendDueAsync"/>.
    /// </returns>
    public async Task ScheduleAsync(InboxMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Delivery != DeliveryState.Delivered || message.RecipientKind != RecipientKind.Patient)
        {
            return;
        }

        var behaviour = _store.GetBehaviour(message.To);
        var number = _store.GetNotificationNumber(message.MessageId);
        var text = ReplyOf(behaviour.Response);
        if (text is null || number != behaviour.RespondToNotification)
        {
            _store.AppendReaction(new PatientReactionRecord(
                message.MessageId, message.To, PatientReactionKind.NoResponse, true, null, _clock.UtcNow, null, true, null));
            return;
        }

        var pending = new PendingResponse(message.MessageId, text, _clock.UtcNow + behaviour.RespondAfter);
        if (behaviour.RespondAfter <= TimeSpan.Zero)
        {
            await SendAsync(pending, ct);
        }
        else
        {
            _store.SchedulePendingResponse(pending);
        }
    }

    /// <summary>
    /// Sends every queued response whose due time has passed on the simulator clock.
    /// </summary>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>How many queued responses were taken and attempted.</returns>
    public async Task<int> SendDueAsync(CancellationToken ct)
    {
        var due = _store.ClaimDueResponses(_clock.UtcNow);
        foreach (var response in due)
        {
            try
            {
                await SendAsync(response, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The responses were already taken off the queue, so one that fails unexpectedly
                // must not take the rest of the batch with it. It stays visible as a failed reaction.
                var message = _store.FindInboxMessage(response.MessageId);
                _store.AppendReaction(new PatientReactionRecord(
                    response.MessageId, message?.To ?? string.Empty, PatientReactionKind.Replied, true, null, _clock.UtcNow, null, false, ex.Message));
            }
        }

        return due.Count;
    }

    private static string? ReplyOf(PatientResponseBehaviour behaviour) => behaviour switch
    {
        PatientResponseBehaviour.Acknowledge => ReplyCodes.WillCome,
        PatientResponseBehaviour.CallMe => ReplyCodes.CallMe,
        _ => null,
    };

    private Task<ReplyResult> SendAsync(PendingResponse response, CancellationToken ct) =>
        _replies.ReplyAsync(response.MessageId, response.Text, ct, automatic: true);
}

using PatientChannelSimulator.Scenarios;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Outbound;

/// <summary>
/// Sends a simulated patient's reply to a text message or an email back to CareLoop, as typed.
/// </summary>
/// <remarks>
/// Used by the inbox reply route and by the automatic responder. The simulator does not say what a
/// reply means and refuses none for its wording: that judgement is CareLoop's, as it would be with a
/// real SMS or email service. The inbox message is updated only after CareLoop accepted the reply,
/// so the inbox never shows a reply that CareLoop does not know about. Every attempt, accepted or
/// not, is recorded as a reaction.
/// </remarks>
public sealed class ReplySender
{
    private readonly CareLoopCallbackClient _careLoop;
    private readonly SimulatorStore _store;
    private readonly MessageGate _gate;
    private readonly ISimulatorClock _clock;

    /// <summary>
    /// Creates the sender.
    /// </summary>
    /// <param name="careLoop">Sends the reply to CareLoop.</param>
    /// <param name="store">Holds the inbox message to read and update, and the reactions.</param>
    /// <param name="gate">Makes two replies to the same message take turns, so each gets its own identifier.</param>
    /// <param name="clock">Source of the reply time.</param>
    public ReplySender(CareLoopCallbackClient careLoop, SimulatorStore store, MessageGate gate, ISimulatorClock clock)
    {
        _careLoop = careLoop;
        _store = store;
        _gate = gate;
        _clock = clock;
    }

    /// <summary>
    /// Sends one reply to an inbox message.
    /// </summary>
    /// <remarks>
    /// A reply is allowed only on a message for the patient that reached them: delivery was
    /// reported, or the channel stayed silent about it. A caregiver's or a staff member's message
    /// can never be replied to, because only the patient's own reply counts.
    /// </remarks>
    /// <param name="messageId">Identifier of the inbox message being replied to.</param>
    /// <param name="text">The reply exactly as typed.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <param name="automatic">Whether the reply comes from the patient's behaviour rather than from someone typing in the ClientApp.</param>
    /// <returns>How the attempt ended, with the inbox message as it is now.</returns>
    public async Task<ReplyResult> ReplyAsync(string messageId, string text, CancellationToken ct, bool automatic = false)
    {
        ArgumentNullException.ThrowIfNull(text);

        // Read, send and record as one turn per message: a second reply waits and then takes the next identifier.
        using var turn = await _gate.EnterAsync(messageId, ct);

        var message = _store.FindInboxMessage(messageId);
        if (message is null)
        {
            return new ReplyResult(ReplyStatus.NotFound, null, "No inbox message has that identifier.");
        }

        if (message.RecipientKind != RecipientKind.Patient)
        {
            return new ReplyResult(ReplyStatus.NotAllowed, message, "Only a message to the patient can be replied to: nobody else's reply counts as the patient's.");
        }

        if (message.Delivery is not (DeliveryState.Delivered or DeliveryState.Silent))
        {
            return new ReplyResult(ReplyStatus.NotAllowed, message, "The message did not reach the patient, so the patient cannot reply to it.");
        }

        var now = _clock.UtcNow;
        // Every earlier attempt uses up its identifier, accepted or not: CareLoop may have stored one whose
        // answer never arrived, and a new reply under the same identifier would be dropped as a resend.
        var earlierAttempts = _store.GetReactions([message.To])
            .Count(r => r.MessageId == message.MessageId && r.Reaction == PatientReactionKind.Replied && r.SourceEventId is not null);
        var replyId = $"{message.MessageId}-r{Math.Max(message.Replies.Count, earlierAttempts) + 1}";
        var callback = SimulatorCallback.Reply(message, replyId, text, now);

        CallbackSendResult send;
        try
        {
            send = await _careLoop.SendAsync(message.Channel, callback, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or CareLoopNotConfiguredException)
        {
            Record(message, automatic, replyId, now, null, false, ex.Message);
            return new ReplyResult(ReplyStatus.Unreachable, message, ex.Message);
        }

        if (!send.Succeeded)
        {
            var error = $"CareLoop answered {send.StatusCode}.";
            Record(message, automatic, replyId, now, send.StatusCode, false, error);
            return new ReplyResult(ReplyStatus.Rejected, message, error);
        }

        var updated = _store.UpdateInboxMessage(message.MessageId, current => current with
        {
            Replies = [.. current.Replies, new InboxReply(replyId, text, now)],
        });
        Record(message, automatic, replyId, now, send.StatusCode, true, null);
        return new ReplyResult(ReplyStatus.Sent, updated, null);
    }

    private void Record(InboxMessage message, bool automatic, string replyId, DateTimeOffset at, int? statusCode, bool succeeded, string? error) =>
        _store.AppendReaction(new PatientReactionRecord(message.MessageId, message.To, PatientReactionKind.Replied, automatic, replyId, at, statusCode, succeeded, error));
}

/// <summary>
/// How an attempt to send a reply ended.
/// </summary>
public enum ReplyStatus
{
    /// <summary>CareLoop accepted the reply.</summary>
    Sent,

    /// <summary>No inbox message has the given identifier.</summary>
    NotFound,

    /// <summary>The message cannot be replied to: it is not for the patient, or it did not reach them.</summary>
    NotAllowed,

    /// <summary>CareLoop answered with a refusal.</summary>
    Rejected,

    /// <summary>CareLoop could not be reached, or no signing secret is configured.</summary>
    Unreachable,
}

/// <summary>
/// The outcome of sending a reply.
/// </summary>
/// <param name="Status">How the attempt ended.</param>
/// <param name="Message">The inbox message as it is now; <see langword="null"/> when it was not found.</param>
/// <param name="Error">Why the attempt did not succeed; <see langword="null"/> when it did.</param>
public sealed record ReplyResult(ReplyStatus Status, InboxMessage? Message, string? Error);

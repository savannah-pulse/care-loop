using System.Text.Json;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Outbound;

/// <summary>
/// What the simulator posts back to CareLoop about a message: a delivery report or a reply.
/// </summary>
/// <remarks>
/// <para>
/// Plain JSON, camel case, with absent values left out. This is the simulator's own definition of
/// the format: it shares no code with CareLoop.
/// </para>
/// <para>
/// A delivery report has <c>type</c> <c>delivery</c>, the <see cref="MessageId"/> and a
/// <see cref="Status"/> of <c>delivered</c> or <c>failed</c> (with a <see cref="Reason"/>). A reply
/// has <c>type</c> <c>reply</c>, its own <see cref="ReplyId"/>, <see cref="InReplyTo"/>,
/// <see cref="From"/> and the <see cref="Text"/> exactly as typed. Both carry <see cref="At"/> and
/// return the message's <see cref="Metadata"/> unchanged.
/// </para>
/// </remarks>
public sealed record SimulatorCallback
{
    /// <summary>Type of a delivery report.</summary>
    public const string DeliveryType = "delivery";

    /// <summary>Type of a reply.</summary>
    public const string ReplyType = "reply";

    /// <summary>Delivery status: the message reached the recipient.</summary>
    public const string DeliveredStatus = "delivered";

    /// <summary>Delivery status: the message could not be delivered.</summary>
    public const string FailedStatus = "failed";

    /// <summary><see cref="DeliveryType"/> or <see cref="ReplyType"/>.</summary>
    public required string Type { get; init; }

    /// <summary>Delivery report: the message it reports on.</summary>
    public string? MessageId { get; init; }

    /// <summary>Delivery report: <see cref="DeliveredStatus"/> or <see cref="FailedStatus"/>.</summary>
    public string? Status { get; init; }

    /// <summary>Delivery report: why delivery failed.</summary>
    public string? Reason { get; init; }

    /// <summary>Reply: the simulator's identifier of the reply, the same on a resend.</summary>
    public string? ReplyId { get; init; }

    /// <summary>Reply: the message being replied to.</summary>
    public string? InReplyTo { get; init; }

    /// <summary>Reply: the address it came from, which is the address the message was sent to.</summary>
    public string? From { get; init; }

    /// <summary>Reply: the text exactly as typed.</summary>
    public string? Text { get; init; }

    /// <summary>When the delivery or the reply happened.</summary>
    public required DateTimeOffset At { get; init; }

    /// <summary>The metadata of the message, returned unchanged.</summary>
    public JsonElement? Metadata { get; init; }

    /// <summary>
    /// Builds the report that a message was delivered.
    /// </summary>
    /// <param name="message">The message that reached its recipient.</param>
    /// <param name="at">When it was delivered.</param>
    /// <returns>The delivery report. It says nothing about whether anyone read or answered the message.</returns>
    public static SimulatorCallback Delivered(InboxMessage message, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new SimulatorCallback { Type = DeliveryType, MessageId = message.MessageId, Status = DeliveredStatus, At = at, Metadata = message.Metadata };
    }

    /// <summary>
    /// Builds the report that a message could not be delivered.
    /// </summary>
    /// <param name="message">The message that did not reach its recipient.</param>
    /// <param name="at">When delivery failed.</param>
    /// <param name="reason">Why it failed.</param>
    /// <returns>The failed-delivery report.</returns>
    public static SimulatorCallback Failed(InboxMessage message, DateTimeOffset at, string reason)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new SimulatorCallback { Type = DeliveryType, MessageId = message.MessageId, Status = FailedStatus, Reason = reason, At = at, Metadata = message.Metadata };
    }

    /// <summary>
    /// Builds a reply to a message.
    /// </summary>
    /// <param name="message">The message being replied to.</param>
    /// <param name="replyId">The identifier of this reply.</param>
    /// <param name="text">The text exactly as typed; it is not trimmed, corrected or interpreted.</param>
    /// <param name="at">When the reply was sent.</param>
    /// <returns>The reply, from the address the message was sent to.</returns>
    public static SimulatorCallback Reply(InboxMessage message, string replyId, string text, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(replyId);
        ArgumentNullException.ThrowIfNull(text);
        return new SimulatorCallback { Type = ReplyType, ReplyId = replyId, InReplyTo = message.MessageId, From = message.To, Text = text, At = at, Metadata = message.Metadata };
    }

    /// <summary>
    /// The identifier that tells this callback from every other, for the record of reactions.
    /// </summary>
    /// <returns>The reply identifier, or the message identifier with the delivery status.</returns>
    public string SourceEventId() => Type == ReplyType ? ReplyId! : $"{MessageId}/delivery/{Status}";
}

/// <summary>
/// The HTTP outcome of posting a callback to CareLoop.
/// </summary>
/// <param name="StatusCode">HTTP status code returned by CareLoop.</param>
/// <param name="Succeeded">Whether the status code indicates success (2xx).</param>
/// <param name="ResponseBody">Raw response body, typically problem details on rejection; <see langword="null"/> when empty.</param>
public sealed record CallbackSendResult(int StatusCode, bool Succeeded, string? ResponseBody);

using System.Text.Json;
using System.Text.Json.Serialization;

namespace PatientChannelSimulator.State;

/// <summary>
/// One simulated text message or email in a simulated inbox, exactly as CareLoop sent it.
/// </summary>
/// <remarks>
/// Holds only what an SMS or email service would be given: an address, a subject for email, and
/// the rendered text. It is synthetic data and <paramref name="Simulated"/> is always
/// <see langword="true"/> so every surface that shows it can label it as simulated.
/// </remarks>
/// <param name="MessageId">The identifier CareLoop gave the message. A resend carries the same one; delivery reports and replies refer to it.</param>
/// <param name="CaseId">The case the message is about, read from the message's metadata; used only to count the notifications a patient received about one case. <see langword="null"/> when the metadata named none.</param>
/// <param name="To">The synthetic phone number or email address the message was sent to (always prefixed <c>SYN-</c>). A behaviour is looked up by this address.</param>
/// <param name="DisplayName">Visibly synthetic display name of whoever the message is for (always starts with "Synthetic").</param>
/// <param name="Channel">Whether it is a text message or an email.</param>
/// <param name="Subject">The subject line of an email; <see langword="null"/> for a text message.</param>
/// <param name="Language">BCP 47 language tag of the text, as sent by CareLoop.</param>
/// <param name="Body">The message text exactly as CareLoop rendered it; the simulator never rewrites content.</param>
/// <param name="ReceivedAt">When the simulator received the message, taken from the simulator clock.</param>
/// <param name="Delivery">What has been reported to CareLoop about delivery.</param>
/// <param name="DeliveryFailureReason">Reason text reported with a failed delivery; <see langword="null"/> unless <paramref name="Delivery"/> is <see cref="DeliveryState.Failed"/>.</param>
/// <param name="RecipientKind">Whether the message is for the patient, a caregiver or a member of staff. Only a patient's message can be replied to.</param>
/// <param name="Replies">The replies CareLoop accepted for this message, oldest first. The simulator forwards a reply as typed and does not say what it means: that is CareLoop's decision.</param>
/// <param name="Simulated">Visible simulated-data marker; always <see langword="true"/>.</param>
public sealed record InboxMessage(
    string MessageId,
    string? CaseId,
    string To,
    string DisplayName,
    InboxChannel Channel,
    string? Subject,
    string Language,
    string Body,
    DateTimeOffset ReceivedAt,
    DeliveryState Delivery,
    string? DeliveryFailureReason,
    RecipientKind RecipientKind,
    IReadOnlyList<InboxReply> Replies,
    bool Simulated = true)
{
    /// <summary>
    /// The metadata CareLoop attached to the message, kept unread so it can be returned unchanged
    /// on every delivery report and reply, as an SMS or email service returns client metadata.
    /// </summary>
    [JsonIgnore]
    public JsonElement? Metadata { get; init; }
}

/// <summary>
/// One reply to a message, as the simulated patient typed it.
/// </summary>
/// <param name="ReplyId">The simulator's identifier of the reply; a resend of the same reply carries the same one.</param>
/// <param name="Text">The text exactly as typed.</param>
/// <param name="SentAt">When CareLoop accepted the reply.</param>
public sealed record InboxReply(string ReplyId, string Text, DateTimeOffset SentAt);

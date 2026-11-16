namespace CareLoop.Api.Infrastructure.Notifications;

/// <summary>
/// One message as CareLoop posts it to the SMS and email simulator: <c>POST {BaseUrl}/messages</c>, JSON, camel case.
/// </summary>
/// <remarks>
/// This is the whole of what the simulator learns: an address and a text. It carries no FHIR and no clinical
/// detail. A replay carries the same <see cref="MessageId"/>, so the simulator can drop it.
/// </remarks>
public sealed record SimulatorMessage
{
    /// <summary>The notification attempt identifier. Delivery reports and replies refer to it.</summary>
    public required string MessageId { get; init; }

    /// <summary><c>sms</c> or <c>email</c>.</summary>
    public required string Channel { get; init; }

    /// <summary>The phone number or email address the message goes to.</summary>
    public required string To { get; init; }

    /// <summary>The subject line, for email; <see langword="null"/> for a text message.</summary>
    public string? Subject { get; init; }

    /// <summary>The rendered message text.</summary>
    public required string Body { get; init; }

    /// <summary>The language of the text, as a BCP 47 tag.</summary>
    public required string Language { get; init; }

    /// <summary>Values the simulator does not interpret and returns unchanged on every callback about this message.</summary>
    public required SimulatorMetadata Metadata { get; init; }
}

/// <summary>
/// What CareLoop attaches to a message so that a callback can be matched to its case without a lookup table.
/// </summary>
/// <remarks>The simulator treats it as opaque and echoes it, as real SMS and email services do with client metadata.</remarks>
public sealed record SimulatorMetadata
{
    /// <summary>Audience of a message to the patient the case is about.</summary>
    public const string PatientAudience = "patient";

    /// <summary>Audience of a message to a consented caregiver.</summary>
    public const string CaregiverAudience = "caregiver";

    /// <summary>Audience of a message to a member of staff or a staff queue.</summary>
    public const string StaffAudience = "staff";

    /// <summary>The case the message is about.</summary>
    public required string CaseId { get; init; }

    /// <summary>
    /// Who the message was for: <see cref="PatientAudience"/>, <see cref="CaregiverAudience"/> or <see cref="StaffAudience"/>.
    /// </summary>
    /// <remarks>Only a callback about a patient message can change a case: the notification track is the patient's.</remarks>
    public required string Audience { get; init; }
}

/// <summary>
/// What the SMS and email simulator posts back to <c>POST /notifications/inbound/{channel}</c>: a delivery report or a reply.
/// </summary>
/// <remarks>
/// <c>type</c> is <c>delivery</c> or <c>reply</c>. A delivery report carries <see cref="MessageId"/>,
/// <see cref="Status"/> (<c>delivered</c> or <c>failed</c>) and, on failure, <see cref="Reason"/>. A reply carries
/// <see cref="ReplyId"/>, <see cref="InReplyTo"/>, <see cref="From"/> and the <see cref="Text"/> exactly as typed.
/// Both carry <see cref="At"/> and the <see cref="Metadata"/> of the message they are about.
/// </remarks>
public sealed record SimulatorCallback
{
    /// <summary><c>delivery</c> or <c>reply</c>.</summary>
    public string? Type { get; init; }

    /// <summary>Delivery report: the message it reports on.</summary>
    public string? MessageId { get; init; }

    /// <summary>Delivery report: <c>delivered</c> or <c>failed</c>.</summary>
    public string? Status { get; init; }

    /// <summary>Delivery report: why delivery failed.</summary>
    public string? Reason { get; init; }

    /// <summary>Reply: the simulator's identifier of the reply, the same on a redelivery.</summary>
    public string? ReplyId { get; init; }

    /// <summary>Reply: the message being replied to.</summary>
    public string? InReplyTo { get; init; }

    /// <summary>Reply: the phone number or email address it came from.</summary>
    public string? From { get; init; }

    /// <summary>Reply: the text exactly as the person typed it.</summary>
    public string? Text { get; init; }

    /// <summary>When the delivery or the reply happened.</summary>
    public DateTimeOffset? At { get; init; }

    /// <summary>The metadata of the message this callback is about, returned unchanged.</summary>
    public SimulatorMetadata? Metadata { get; init; }
}

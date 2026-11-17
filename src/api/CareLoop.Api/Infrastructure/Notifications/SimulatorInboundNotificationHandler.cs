using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CareLoop.Api.Features.Notifications;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Events;
using CareLoop.Domain.Recipients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Infrastructure.Notifications;

/// <summary>
/// Understands what the messaging simulator sends back to CareLoop: delivery reports and replies.
/// </summary>
/// <remarks>
/// <para>
/// The body is a <see cref="SimulatorCallback"/> as JSON. A <c>delivery</c> with status <c>delivered</c> is a
/// delivery and <c>failed</c> (with its <c>reason</c>) a failure; any other status is not tracked. A
/// <c>reply</c> carries the text exactly as the patient typed it, and this handler says what it means:
/// <see cref="AcknowledgeReply"/> is an acknowledgement ("I got this message"), <see cref="CallMeReply"/> asks for a
/// call, and any other text asks for a call too, because an unreadable reply is a patient trying to say
/// something. A delivery report is never an acknowledgement, and no reply is ever teach-back or a confidence
/// rating: a nurse records those after a call.
/// </para>
/// <para>
/// Every message must be signed: header <see cref="SignatureHeader"/> carries <c>sha256=</c> followed by the lower-case
/// hexadecimal HMAC-SHA256 of the raw body (UTF-8), keyed with the channel's <see cref="SimulatorNotificationOptions.InboundSecret"/>.
/// A missing or wrong signature, or no secret configured, is refused before the body is read, so nobody who merely
/// knows a case identifier can acknowledge for a patient.
/// </para>
/// <para>
/// The callback echoes the <see cref="SimulatorMetadata"/> of the message it is about. A callback about a caregiver
/// or staff message translates to nothing: the case's notification track is the patient's, and nobody else's
/// delivery or reply may count as hers. For a patient message, the message identifier must be an attempt CareLoop
/// made for a patient notification of that case on this channel, and a reply must come from one of the patient's
/// own addresses on this channel; anything else is a format error, so a callback can never change a case it was
/// not sent for. A reply to the appointment message counts like a reply to the follow-up; that message's delivery
/// reports are ignored, because the case's notification track follows the follow-up message only.
/// </para>
/// <para>
/// The source event identifier is built from the message identifier and status, or from the reply identifier, so
/// a redelivery is deduplicated.
/// </para>
/// </remarks>
public abstract class SimulatorInboundNotificationHandler : IInboundNotificationHandler
{
    /// <summary>The header that carries the message signature.</summary>
    public const string SignatureHeader = "X-CareLoop-Signature";

    /// <summary>The reply that means "I got this message": an acknowledgement.</summary>
    public const string AcknowledgeReply = "1";

    /// <summary>The reply that asks the care team for a call.</summary>
    public const string CallMeReply = "2";

    /// <summary>Callback type of a delivery report.</summary>
    public const string DeliveryType = "delivery";

    /// <summary>Callback type of a reply.</summary>
    public const string ReplyType = "reply";

    /// <summary>Delivery status: the message reached the recipient's device or mailbox.</summary>
    public const string DeliveredStatus = "delivered";

    /// <summary>Delivery status: the message could not be delivered.</summary>
    public const string FailedStatus = "failed";

    private const string SignaturePrefix = "sha256=";
    private const string DefaultFailureReason = "The simulator reported the message as not delivered.";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly CareLoopDbContext _db;
    private readonly RecipientResolver _recipients;
    private readonly IOptionsMonitor<SimulatorNotificationOptions> _options;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="db">Read access to cases, to find the source identity of the case a callback is about.</param>
    /// <param name="recipients">Supplies the patient's own addresses, which a reply must come from.</param>
    /// <param name="options">Supplies the secret that inbound messages are signed with, one named set per channel.</param>
    protected SimulatorInboundNotificationHandler(CareLoopDbContext db, RecipientResolver recipients, IOptionsMonitor<SimulatorNotificationOptions> options)
    {
        _db = db;
        _recipients = recipients;
        _options = options;
    }

    /// <summary>
    /// Computes the signature header value for a body, as the simulator must send it.
    /// </summary>
    /// <param name="body">The raw request body.</param>
    /// <param name="secret">The shared secret.</param>
    /// <returns><c>sha256=</c> followed by the lower-case hexadecimal HMAC-SHA256 of the body.</returns>
    public static string SignatureFor(string body, string secret)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentException.ThrowIfNullOrEmpty(secret);
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body));
        return SignaturePrefix + Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Says what a reply means.
    /// </summary>
    /// <param name="text">The reply exactly as typed.</param>
    /// <returns>
    /// <see cref="NotificationUpdateKind.Acknowledged"/> for <see cref="AcknowledgeReply"/>, ignoring surrounding
    /// white space; <see cref="NotificationUpdateKind.CallRequested"/> for <see cref="CallMeReply"/> and for any other text.
    /// </returns>
    public static NotificationUpdateKind KindOfReply(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return string.Equals(text.Trim(), AcknowledgeReply, StringComparison.Ordinal)
            ? NotificationUpdateKind.Acknowledged
            : NotificationUpdateKind.CallRequested;
    }

    /// <inheritdoc/>
    public abstract NotificationChannel Channel { get; }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EventEnvelope>> TranslateAsync(InboundNotificationMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        Verify(message);
        var callback = Parse(message.Body);

        var isDelivery = string.Equals(callback.Type, DeliveryType, StringComparison.OrdinalIgnoreCase);
        var isReply = string.Equals(callback.Type, ReplyType, StringComparison.OrdinalIgnoreCase);
        if (!isDelivery && !isReply)
        {
            // A kind of callback CareLoop does not track.
            return [];
        }

        var metadata = callback.Metadata
            ?? throw new FormatException("The callback has no metadata, so the case it is about is not known.");
        if (!string.Equals(metadata.Audience, SimulatorMetadata.PatientAudience, StringComparison.OrdinalIgnoreCase))
        {
            // A caregiver's or a staff member's message: nothing about it may count as the patient's.
            return [];
        }

        NotificationUpdateKind kind;
        if (isDelivery)
        {
            if (string.Equals(callback.Status, DeliveredStatus, StringComparison.OrdinalIgnoreCase))
            {
                kind = NotificationUpdateKind.Delivered;
            }
            else if (string.Equals(callback.Status, FailedStatus, StringComparison.OrdinalIgnoreCase))
            {
                kind = NotificationUpdateKind.Failed;
            }
            else
            {
                // An in-between status (queued, sent) that CareLoop does not track.
                return [];
            }
        }
        else
        {
            kind = KindOfReply(callback.Text ?? throw new FormatException("The reply has no text."));
        }

        if (!Guid.TryParse(metadata.CaseId, out var caseId))
        {
            throw new FormatException("The callback's metadata does not name a case.");
        }

        var messageId = isDelivery ? callback.MessageId : callback.InReplyTo;
        if (!Guid.TryParse(messageId, out var attemptId))
        {
            throw new FormatException(isDelivery
                ? "The delivery report does not name the message it reports on."
                : "The reply does not name the message it replies to.");
        }

        if (isReply && string.IsNullOrWhiteSpace(callback.ReplyId))
        {
            throw new FormatException("The reply has no identifier, so a redelivery could not be recognised.");
        }

        var occurredAt = callback.At ?? throw new FormatException("The callback does not say when it happened.");

        var source = await _db.Cases.AsNoTracking()
            .Where(c => c.Id == caseId)
            .Select(c => new { c.SourceSystem, c.SourceResultId, c.PatientReference })
            .SingleOrDefaultAsync(ct)
            ?? throw new FormatException($"The callback is about case {caseId:D}, which is not a known case.");

        var sentFor = await PatientMessageOfCaseAsync(caseId, source.PatientReference, attemptId, ct);
        if (sentFor is null)
        {
            throw new FormatException($"Message {attemptId:D} is not a patient message of case {caseId:D} on this channel.");
        }

        if (isDelivery && sentFor == nameof(Domain.Events.DomainEvents.AppointmentScheduled))
        {
            // The appointment message is not the follow-up notification the track follows: its delivery changes nothing.
            // A reply to it still counts below, as any reply of the patient's does.
            return [];
        }

        var channel = SimulatorNotificationProvider.WireName(Channel);
        if (isReply && !await IsFromThePatientAsync(caseId, callback.From, ct))
        {
            throw new FormatException("The reply did not come from an address of the patient on this channel.");
        }

        var reference = isDelivery
            ? $"{channel}/message/{attemptId:D}"
            : $"{channel}/reply/{callback.ReplyId!.Trim()}";
        return
        [
            new EventEnvelope
            {
                SourceSystem = source.SourceSystem,
                Origin = channel,
                SourceEventId = isDelivery ? $"{reference}/delivery/{callback.Status!.ToLowerInvariant()}" : reference,
                Type = CaseEventType.NotificationUpdated,
                SourceResultId = source.SourceResultId,
                OccurredAt = occurredAt,
                Actor = null,
                Payload = new NotificationUpdatePayload
                {
                    Kind = kind,
                    Channel = Channel,
                    CommunicationReference = reference,
                    AttemptId = attemptId,
                    FailureReason = kind == NotificationUpdateKind.Failed
                        ? (string.IsNullOrWhiteSpace(callback.Reason) ? DefaultFailureReason : callback.Reason.Trim())
                        : null,
                },
            },
        ];
    }

    private static SimulatorCallback Parse(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<SimulatorCallback>(body, Json)
                ?? throw new FormatException("The body is empty.");
        }
        catch (JsonException ex)
        {
            throw new FormatException($"The body is not a valid simulator callback: {ex.Message}", ex);
        }
    }

    /// <exception cref="UnauthorizedAccessException">No secret is configured, or the signature is missing or wrong.</exception>
    private void Verify(InboundNotificationMessage message)
    {
        var secret = _options.Get(Channel.ToString()).InboundSecret;
        if (string.IsNullOrEmpty(secret))
        {
            throw new UnauthorizedAccessException($"{SimulatorNotificationOptions.SectionFor(Channel)}:InboundSecret is not configured, so no inbound message can be trusted.");
        }

        var given = message.Headers.FirstOrDefault(h => string.Equals(h.Key, SignatureHeader, StringComparison.OrdinalIgnoreCase)).Value;
        var expected = SignatureFor(message.Body, secret);
        if (string.IsNullOrEmpty(given)
            || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(given.Trim().ToLowerInvariant()), Encoding.ASCII.GetBytes(expected)))
        {
            throw new UnauthorizedAccessException("The message signature is missing or does not match.");
        }
    }

    /// <summary>
    /// Finds which patient message of this case on this channel an attempt identifier belongs to.
    /// </summary>
    /// <remarks>
    /// Attempt identifiers are derived from the domain event that caused the message, the patient and the channel
    /// (<see cref="NotificationSender.AttemptIdFor"/>), and every such event is in the case's outbox. Recomputing them
    /// means a callback whose metadata and message identifier disagree can never change the wrong case, without storing
    /// sent attempts. The patient is written to for the follow-up (<see cref="Domain.Events.DomainEvents.NotificationRequested"/>)
    /// and when an appointment is booked (<see cref="Domain.Events.DomainEvents.AppointmentScheduled"/>).
    /// </remarks>
    /// <returns>The name of the event the message was sent for, or <see langword="null"/> when it is none of the patient's.</returns>
    private async Task<string?> PatientMessageOfCaseAsync(Guid caseId, string patientReference, Guid attemptId, CancellationToken ct)
    {
        string[] patientEvents = [nameof(Domain.Events.DomainEvents.NotificationRequested), nameof(Domain.Events.DomainEvents.AppointmentScheduled)];
        var sent = await _db.OutboxMessages.AsNoTracking()
            .Where(m => m.CaseId == caseId && patientEvents.Contains(m.EventType))
            .Select(m => new { m.Id, m.EventType })
            .ToListAsync(ct);
        return sent.FirstOrDefault(m => NotificationSender.AttemptIdFor(m.Id, patientReference, Channel) == attemptId)?.EventType;
    }

    private async Task<bool> IsFromThePatientAsync(Guid caseId, string? from, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(from))
        {
            return false;
        }

        var contactPoints = await _recipients.PatientContactPointsAsync(caseId, ct);
        return contactPoints.Any(c => c.Channel == Channel
            && string.Equals(c.Address.Trim(), from.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}

using System.Text.Json;
using PatientChannelSimulator.Outbound;
using PatientChannelSimulator.Scenarios;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Inbound;

/// <summary>
/// One text message or email as CareLoop posts it to <c>POST /messages</c>.
/// </summary>
/// <remarks>
/// Plain JSON, camel case: what an SMS or email service is given. This is the simulator's own
/// definition of the format; it shares no code with CareLoop.
/// </remarks>
/// <param name="MessageId">The identifier CareLoop gave the message. A resend carries the same one.</param>
/// <param name="Channel"><c>sms</c> or <c>email</c>.</param>
/// <param name="To">The synthetic phone number or email address to send to; must start with <c>SYN-</c>.</param>
/// <param name="Subject">The subject line, for email.</param>
/// <param name="Body">The message text.</param>
/// <param name="Language">The language of the text, as a BCP 47 tag.</param>
/// <param name="Metadata">Values the simulator returns unchanged on every callback. It reads only <c>caseId</c> and <c>audience</c> (<c>patient</c>, <c>caregiver</c> or <c>staff</c>) from it, to file the message in the right inbox.</param>
public sealed record IncomingMessage(
    string? MessageId,
    string? Channel,
    string? To,
    string? Subject,
    string? Body,
    string? Language,
    JsonElement? Metadata);

/// <summary>
/// What <c>POST /messages</c> answers when it accepts a message.
/// </summary>
/// <param name="MessageId">The identifier of the accepted message.</param>
/// <param name="Status">Always <c>accepted</c>: what happens next is reported by a callback.</param>
/// <param name="Simulated">Visible simulated-data marker; always <see langword="true"/>.</param>
public sealed record MessageAccepted(string MessageId, string Status = "accepted", bool Simulated = true);

/// <summary>
/// The outcome of receiving one message.
/// </summary>
/// <param name="Message">The inbox message, as it is after the delivery outcome was simulated.</param>
/// <param name="Created">Whether this call created it; <see langword="false"/> for a resend.</param>
public sealed record ReceivedMessage(InboxMessage Message, bool Created);

/// <summary>
/// Handles a text message or an email posted by CareLoop.
/// </summary>
/// <remarks>
/// <para>
/// Checks the message, places it in the simulated inbox, hands it to <see cref="DeliverySimulator"/>
/// and then, if it was delivered, to <see cref="AutomaticResponder"/> so the patient can react.
/// Handling is idempotent on the message identifier: a resend must not create a second inbox
/// message, a second delivery report or a second reply. The one thing a resend does is finish a
/// delivery that is still unreported.
/// </para>
/// <para>
/// When the delivery outcome cannot be reported, the message is still accepted, as a real service
/// would accept it, and <see cref="DeliveryReportRetrier"/> reports later. CareLoop does not send
/// a patient notification again, so refusing the message would leave it undelivered for good.
/// </para>
/// <para>
/// The exchange is logged once, with the status CareLoop actually received. A message that is
/// refused is logged without its body: it was not accepted as synthetic, so it is not kept.
/// </para>
/// </remarks>
public sealed class MessageReceiver
{
    /// <summary>The route CareLoop posts messages to; used as the URL of the log entry.</summary>
    public const string Route = "/messages";

    /// <summary>The <c>channel</c> value of a text message.</summary>
    public const string SmsChannel = "sms";

    /// <summary>The <c>channel</c> value of an email.</summary>
    public const string EmailChannel = "email";

    /// <summary>The language recorded when a message states none.</summary>
    public const string UndeterminedLanguage = "und";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly DeliverySimulator _delivery;
    private readonly AutomaticResponder _responder;
    private readonly DeliveryReportRetrier _retrier;
    private readonly SimulatorStore _store;
    private readonly MessageGate _gate;
    private readonly ISimulatorClock _clock;

    /// <summary>
    /// Creates the receiver.
    /// </summary>
    /// <param name="delivery">Simulates the delivery outcome once the message is stored.</param>
    /// <param name="responder">Lets the patient react to a delivered message according to their behaviour.</param>
    /// <param name="retrier">Reports the delivery outcome later when it cannot be reported now.</param>
    /// <param name="store">Holds the inbox and the message log.</param>
    /// <param name="gate">Keeps a resend from reporting delivery while the first attempt is still doing so.</param>
    /// <param name="clock">Source of the received time.</param>
    public MessageReceiver(
        DeliverySimulator delivery,
        AutomaticResponder responder,
        DeliveryReportRetrier retrier,
        SimulatorStore store,
        MessageGate gate,
        ISimulatorClock clock)
    {
        _delivery = delivery;
        _responder = responder;
        _retrier = retrier;
        _store = store;
        _gate = gate;
        _clock = clock;
    }

    /// <summary>
    /// Turns a posted message into an inbox message.
    /// </summary>
    /// <param name="incoming">The message as posted.</param>
    /// <param name="receivedAt">When it was received.</param>
    /// <returns>The inbox message, with its delivery still pending.</returns>
    /// <exception cref="FormatException">
    /// The message has no identifier, text or known channel; its address is not synthetic; or its
    /// metadata names an audience other than <c>patient</c>, <c>caregiver</c> or <c>staff</c>.
    /// </exception>
    public static InboxMessage Map(IncomingMessage incoming, DateTimeOffset receivedAt)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        if (string.IsNullOrWhiteSpace(incoming.MessageId))
        {
            throw new FormatException("The message has no messageId, so a resend could not be recognised.");
        }

        InboxChannel channel = incoming.Channel?.Trim().ToLowerInvariant() switch
        {
            SmsChannel => InboxChannel.Sms,
            EmailChannel => InboxChannel.Email,
            _ => throw new FormatException($"The channel must be '{SmsChannel}' or '{EmailChannel}'."),
        };

        var to = incoming.To?.Trim();
        if (to is null || !PatientBehaviourRules.IsSynthetic(to))
        {
            throw new FormatException($"The address is not synthetic: it must start with {PatientBehaviourRules.SyntheticPrefix}. This simulator delivers nothing to a real phone number or email address.");
        }

        if (string.IsNullOrWhiteSpace(incoming.Body))
        {
            throw new FormatException("The message has no body.");
        }

        var kind = AudienceOf(incoming.Metadata);
        return new InboxMessage(
            incoming.MessageId.Trim(),
            StringIn(incoming.Metadata, "caseId"),
            to,
            kind switch
            {
                RecipientKind.Caregiver => $"Synthetic caregiver {to}",
                RecipientKind.Staff => $"Synthetic staff {to}",
                _ => $"Synthetic patient {to}",
            },
            channel,
            channel == InboxChannel.Email && !string.IsNullOrWhiteSpace(incoming.Subject) ? incoming.Subject.Trim() : null,
            string.IsNullOrWhiteSpace(incoming.Language) ? UndeterminedLanguage : incoming.Language.Trim(),
            incoming.Body,
            receivedAt,
            DeliveryState.Pending,
            DeliveryFailureReason: null,
            kind,
            Replies: [])
        {
            Metadata = incoming.Metadata?.Clone(),
        };
    }

    /// <summary>
    /// Receives one message.
    /// </summary>
    /// <param name="rawBody">The request body exactly as it arrived: an <see cref="IncomingMessage"/> as JSON.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The inbox message for the request, and whether this call created it.</returns>
    /// <exception cref="FormatException">The body is not JSON, or the message cannot be accepted; see <see cref="Map"/>.</exception>
    /// <exception cref="HttpRequestException">The delivery outcome could not be reported to CareLoop and background retries are switched off. The message is in the inbox, still pending; a resend tries again.</exception>
    /// <exception cref="CareLoopNotConfiguredException">No signing secret is configured, so the delivery outcome cannot be reported at all. The message is in the inbox, still pending.</exception>
    public async Task<ReceivedMessage> ReceiveAsync(string rawBody, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(rawBody);
        var now = _clock.UtcNow;
        InboxMessage mapped;
        try
        {
            mapped = Map(Parse(rawBody), now);
        }
        catch (FormatException)
        {
            // Refused, so not kept: the body was never accepted as synthetic data.
            Log(now, null, StatusCodes.Status400BadRequest, null);
            throw;
        }

        var created = _store.TryAddInboxMessage(mapped, out var message);
        var status = StatusCodes.Status502BadGateway;
        try
        {
            // A resend changes nothing, unless the delivery outcome is still unreported. Only one
            // request at a time may find that out: a resend that arrives while the first attempt
            // is still reporting waits, and then sees what that attempt achieved.
            var deliveredNow = false;
            using (await _gate.EnterAsync(message.MessageId, ct))
            {
                message = _store.FindInboxMessage(message.MessageId) ?? message;
                if (message.Delivery == DeliveryState.Pending)
                {
                    try
                    {
                        message = await _delivery.ApplyAsync(message, ct);
                        deliveredNow = message.Delivery == DeliveryState.Delivered;
                    }
                    catch (HttpRequestException) when (_retrier.ScheduleFirst(message.MessageId))
                    {
                        // Accepted and still pending; the report is tried again in the background.
                    }
                }
            }

            // Outside the gate, because replying enters it again. Only the request that
            // reported the delivery schedules the patient's reply, so it is scheduled once.
            if (deliveredNow)
            {
                await _responder.ScheduleAsync(message, ct);
                message = _store.FindInboxMessage(message.MessageId) ?? message;
            }

            status = created ? StatusCodes.Status201Created : StatusCodes.Status200OK;
            return new ReceivedMessage(message, created);
        }
        finally
        {
            Log(now, message.MessageId, status, rawBody);
        }
    }

    private static IncomingMessage Parse(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<IncomingMessage>(body, Json)
                ?? throw new FormatException("The body is empty.");
        }
        catch (JsonException ex)
        {
            throw new FormatException($"The body is not a valid message: {ex.Message}", ex);
        }
    }

    private static RecipientKind AudienceOf(JsonElement? metadata) => StringIn(metadata, "audience")?.Trim().ToLowerInvariant() switch
    {
        // A message that names no audience is taken to be for a patient, as a plain SMS or email would be.
        null or "" or "patient" => RecipientKind.Patient,
        "caregiver" => RecipientKind.Caregiver,
        "staff" => RecipientKind.Staff,
        var other => throw new FormatException($"'{other}' is not a known audience: use patient, caregiver or staff."),
    };

    private static string? StringIn(JsonElement? metadata, string name) =>
        metadata is { ValueKind: JsonValueKind.Object } element
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private void Log(DateTimeOffset at, string? messageId, int statusCode, string? body) =>
        _store.AppendLogEntry(new MessageLogEntry(
            Guid.NewGuid().ToString("N"),
            at,
            MessageDirection.Inbound,
            "POST",
            Route,
            MessageLogEntry.MessageKind,
            messageId,
            statusCode,
            body));
}

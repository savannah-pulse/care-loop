using System.Net.Http.Json;
using System.Text.Json;
using CareLoop.Api.Features.Notifications;
using CareLoop.Domain.Recipients;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Infrastructure.Notifications;

/// <summary>
/// Sends a message to the SMS and email simulator, the way an SMS or email service is called.
/// </summary>
/// <remarks>
/// <para>
/// The text-message and email providers share this: each posts a <see cref="SimulatorMessage"/> as JSON to
/// <c>{BaseUrl}/messages</c> of its channel's simulator (<see cref="SimulatorNotificationOptions"/>). No FHIR travels on
/// this path. The simulator answers 2xx when it accepted the message; what happens next (delivery, failure, a
/// reply) comes back through <see cref="SimulatorInboundNotificationHandler"/>.
/// </para>
/// <para>
/// This is a simulation: the format is CareLoop's own, and both channels are served by the SMS and email
/// simulator. Real delivery means a provider written for a real service's API beside this one; the sender and the
/// router do not change.
/// </para>
/// </remarks>
public abstract class SimulatorNotificationProvider : INotificationProvider
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IOptionsMonitor<SimulatorNotificationOptions> _options;

    /// <summary>
    /// Creates the provider.
    /// </summary>
    /// <param name="http">The HTTP client supplied by the HTTP client factory.</param>
    /// <param name="options">The simulator settings, one named set per channel.</param>
    protected SimulatorNotificationProvider(HttpClient http, IOptionsMonitor<SimulatorNotificationOptions> options)
    {
        _http = http;
        _options = options;
    }

    /// <inheritdoc/>
    public abstract NotificationChannel Channel { get; }

    /// <inheritdoc/>
    public abstract bool AllowsSensitiveContent { get; }

    /// <summary>
    /// The channel as the simulator names it.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <returns><c>sms</c> or <c>email</c>.</returns>
    public static string WireName(NotificationChannel channel) => channel.ToString().ToLowerInvariant();

    /// <summary>
    /// The audience label carried in a message's metadata.
    /// </summary>
    /// <param name="kind">The kind of recipient.</param>
    /// <returns><c>patient</c>, <c>caregiver</c> or <c>staff</c>.</returns>
    public static string AudienceOf(RecipientKind kind) => kind switch
    {
        RecipientKind.Patient => SimulatorMetadata.PatientAudience,
        RecipientKind.Caregiver => SimulatorMetadata.CaregiverAudience,
        _ => SimulatorMetadata.StaffAudience,
    };

    /// <inheritdoc/>
    public bool CanReach(Recipient recipient)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        return recipient.ContactPoints.Any(c => c.Channel == Channel && !string.IsNullOrWhiteSpace(c.Address));
    }

    /// <summary>
    /// Builds the message posted to the simulator.
    /// </summary>
    /// <param name="message">The rendered message.</param>
    /// <returns>The simulator message: the attempt identifier, the address, the text and the metadata to echo.</returns>
    /// <remarks>A text message has no subject line, so a subject is carried on email only.</remarks>
    public SimulatorMessage SimulatorMessageFor(NotificationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new SimulatorMessage
        {
            MessageId = message.AttemptId.ToString("D"),
            Channel = WireName(Channel),
            To = message.ContactPoint.Address,
            Subject = Channel == NotificationChannel.Email ? message.Subject : null,
            Body = message.Body,
            Language = message.Recipient.Language,
            Metadata = new SimulatorMetadata
            {
                CaseId = message.CaseId.ToString("D"),
                Audience = AudienceOf(message.Recipient.Kind),
            },
        };
    }

    /// <inheritdoc/>
    public async Task<SendResult> SendAsync(NotificationMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);

        var baseUrl = _options.Get(Channel.ToString()).BaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return Refused($"{SimulatorNotificationOptions.SectionFor(Channel)}:{nameof(SimulatorNotificationOptions.BaseUrl)} is not configured.");
        }

        var simulatorMessage = SimulatorMessageFor(message);
        try
        {
            using var response = await _http.PostAsJsonAsync($"{baseUrl.TrimEnd('/')}/messages", simulatorMessage, Json, ct);
            return response.IsSuccessStatusCode
                ? new SendResult { Accepted = true, ProviderMessageId = $"{simulatorMessage.Channel}/{simulatorMessage.MessageId}" }
                : Refused($"The {simulatorMessage.Channel} simulator answered {(int)response.StatusCode} {response.ReasonPhrase}.");
        }
        catch (HttpRequestException ex)
        {
            return Refused($"The {simulatorMessage.Channel} simulator could not be reached: {ex.Message}");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return Refused($"The {simulatorMessage.Channel} simulator did not answer in time.");
        }
    }

    private static SendResult Refused(string reason) => new() { Accepted = false, FailureReason = reason };
}

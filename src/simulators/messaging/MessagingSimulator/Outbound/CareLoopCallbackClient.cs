using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MessagingSimulator.Scenarios;
using MessagingSimulator.State;

namespace MessagingSimulator.Outbound;

/// <summary>
/// Typed HTTP client for everything the simulator sends to CareLoop: delivery reports and replies.
/// </summary>
/// <remarks>
/// Each callback is posted as JSON to <c>{CallbackBaseUrl}/sms</c> or <c>{CallbackBaseUrl}/email</c>,
/// the way an SMS or email service calls its customer back, and every one of them is signed. Every
/// exchange is recorded in the message log. The simulator reads nothing from CareLoop.
/// </remarks>
public sealed class CareLoopCallbackClient
{
    /// <summary>The header that carries the signature of a callback body.</summary>
    public const string SignatureHeader = "X-CareLoop-Signature";

    /// <summary>The media type of a callback.</summary>
    public const string MediaType = "application/json";

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly SimulatorStore _store;
    private readonly ISimulatorClock _clock;
    private readonly IOptions<CareLoopOptions> _options;

    /// <summary>
    /// Creates the client.
    /// </summary>
    /// <param name="http">HTTP client used for the callbacks.</param>
    /// <param name="store">Receives a log entry for every exchange.</param>
    /// <param name="clock">Source of the timestamps on log entries.</param>
    /// <param name="options">Supplies the callback address and the signing secret.</param>
    public CareLoopCallbackClient(HttpClient http, SimulatorStore store, ISimulatorClock clock, IOptions<CareLoopOptions> options)
    {
        _http = http;
        _store = store;
        _clock = clock;
        _options = options;
    }

    /// <summary>
    /// Computes the signature CareLoop expects for a callback body.
    /// </summary>
    /// <param name="body">The exact bytes that will be sent.</param>
    /// <param name="secret">The shared secret.</param>
    /// <returns><c>sha256=</c> followed by the lower-case hexadecimal HMAC-SHA256 of the body, keyed with the UTF-8 secret.</returns>
    public static string SignatureFor(ReadOnlySpan<byte> body, string secret)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);
        return "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Utf8NoBom.GetBytes(secret), body));
    }

    /// <summary>
    /// The last segment of the callback address for a channel.
    /// </summary>
    /// <param name="channel">The channel the message was sent on.</param>
    /// <returns><c>sms</c> or <c>email</c>.</returns>
    public static string ChannelSegment(InboxChannel channel) => channel == InboxChannel.Sms ? "sms" : "email";

    /// <summary>
    /// Sends a delivery report or a reply to CareLoop with <c>POST {CallbackBaseUrl}/{channel}</c>,
    /// signed with <see cref="CareLoopOptions.InboundSecret"/>.
    /// </summary>
    /// <remarks>
    /// The callback is serialized once and those same bytes are signed and sent, so the signature
    /// can never be computed over anything but what CareLoop receives. A server error or a timeout
    /// is retried up to <see cref="CareLoopOptions.SendAttempts"/> times; that is safe because
    /// CareLoop deduplicates on the message identifier and status, or on the reply identifier. A
    /// refusal (4xx) is never retried.
    /// </remarks>
    /// <param name="channel">The channel the message was sent on, which decides the callback address.</param>
    /// <param name="callback">The delivery report or reply to send.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The HTTP outcome. A rejected callback is reported in the result, not thrown.</returns>
    /// <exception cref="CareLoopNotConfiguredException">No signing secret is configured, so nothing was sent.</exception>
    /// <exception cref="HttpRequestException">CareLoop could not be reached at all.</exception>
    public async Task<CallbackSendResult> SendAsync(InboxChannel channel, SimulatorCallback callback, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var options = _options.Value;
        if (string.IsNullOrEmpty(options.InboundSecret))
        {
            throw new CareLoopNotConfiguredException(
                $"{CareLoopOptions.SectionName}:{nameof(CareLoopOptions.InboundSecret)} is not configured, so CareLoop would refuse the callback. Nothing was sent.");
        }

        var json = JsonSerializer.Serialize(callback, Json);
        var bytes = Utf8NoBom.GetBytes(json);
        var signature = SignatureFor(bytes, options.InboundSecret);
        var url = $"{options.CallbackBaseUrl.TrimEnd('/')}/{ChannelSegment(channel)}";
        var kind = callback.Type == SimulatorCallback.ReplyType ? MessageLogEntry.ReplyKind : MessageLogEntry.DeliveryKind;
        var messageId = callback.MessageId ?? callback.InReplyTo;
        var attempts = Math.Max(1, options.SendAttempts);

        for (var attempt = 1; ; attempt++)
        {
            var last = attempt >= attempts;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = new ByteArrayContent(bytes);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(MediaType) { CharSet = "utf-8" };
                request.Headers.TryAddWithoutValidation(SignatureHeader, signature);
                using var response = await _http.SendAsync(request, ct);
                var responseBody = await response.Content.ReadAsStringAsync(ct);
                Log(url, kind, messageId, (int)response.StatusCode, json);

                if ((int)response.StatusCode < 500 || last)
                {
                    return new CallbackSendResult(
                        (int)response.StatusCode,
                        response.IsSuccessStatusCode,
                        string.IsNullOrEmpty(responseBody) ? null : responseBody);
                }
            }
            catch (HttpRequestException) when (!last)
            {
                Log(url, kind, messageId, null, json);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested && !last)
            {
                Log(url, kind, messageId, null, json);
            }
            catch (HttpRequestException)
            {
                Log(url, kind, messageId, null, json);
                throw;
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                Log(url, kind, messageId, null, json);
                throw new HttpRequestException("CareLoop did not answer in time.", ex);
            }

            await Task.Delay(TimeSpan.FromMilliseconds((long)Math.Max(0, options.RetryDelayMilliseconds) * attempt), ct);
        }
    }

    private void Log(string url, string kind, string? messageId, int? statusCode, string? body) =>
        _store.AppendLogEntry(new MessageLogEntry(
            Guid.NewGuid().ToString("N"),
            _clock.UtcNow,
            MessageDirection.Outbound,
            "POST",
            url,
            kind,
            messageId,
            statusCode,
            body));
}

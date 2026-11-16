using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Options;
using PatientChannelSimulator.Scenarios;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Outbound;

/// <summary>
/// Typed HTTP client for everything the simulator sends to or reads from CareLoop.
/// </summary>
/// <remarks>
/// Serializes and parses FHIR itself with the Firely SDK, as a real patient channel would, and
/// records every exchange in the FHIR message log. Case status is read from
/// <see cref="CareLoopOptions.FhirBaseUrl"/>; delivery reports and patient responses are posted
/// to <see cref="CareLoopOptions.NotificationCallbackUrl"/>, the callback of CareLoop's
/// simulator notification provider, and every one of them is signed.
/// </remarks>
public sealed class CareLoopFhirClient
{
    /// <summary>The header that carries the signature of a callback body.</summary>
    public const string SignatureHeader = "X-CareLoop-Signature";

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly HttpClient _http;
    private readonly SimulatorStore _store;
    private readonly ISimulatorClock _clock;
    private readonly IOptions<CareLoopOptions> _options;

    /// <summary>
    /// Creates the client.
    /// </summary>
    /// <param name="http">HTTP client whose base address is CareLoop's FHIR base URL.</param>
    /// <param name="store">Receives a log entry for every exchange.</param>
    /// <param name="clock">Source of the timestamps on log entries.</param>
    /// <param name="options">Supplies the notification callback address and the signing secret.</param>
    public CareLoopFhirClient(HttpClient http, SimulatorStore store, ISimulatorClock clock, IOptions<CareLoopOptions> options)
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
    /// Sends a <c>Communication</c> (delivery report or patient response) to CareLoop with
    /// <c>POST {NotificationCallbackUrl}</c>, signed with <see cref="CareLoopOptions.InboundSecret"/>.
    /// </summary>
    /// <remarks>
    /// The resource is serialized once and those same bytes are signed and sent, so the signature
    /// can never be computed over anything but what CareLoop receives. A server error or a timeout
    /// is retried up to <see cref="CareLoopOptions.SendAttempts"/> times; that is safe because
    /// CareLoop deduplicates on the resource id and version. A refusal (4xx) is never retried.
    /// </remarks>
    /// <param name="communication">The resource to send, produced by one of the builders.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The HTTP outcome. A rejected resource is reported in the result, not thrown.</returns>
    /// <exception cref="CareLoopNotConfiguredException">No signing secret is configured, so nothing was sent.</exception>
    /// <exception cref="HttpRequestException">CareLoop could not be reached at all.</exception>
    public async System.Threading.Tasks.Task<FhirSendResult> SendCommunicationAsync(Communication communication, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(communication);
        var options = _options.Value;
        if (string.IsNullOrEmpty(options.InboundSecret))
        {
            throw new CareLoopNotConfiguredException(
                $"{CareLoopOptions.SectionName}:{nameof(CareLoopOptions.InboundSecret)} is not configured, so CareLoop would refuse the callback. Nothing was sent.");
        }

        var json = FhirJson.Serialize(communication);
        var bytes = Utf8NoBom.GetBytes(json);
        var signature = SignatureFor(bytes, options.InboundSecret);
        var url = options.NotificationCallbackUrl;
        var attempts = Math.Max(1, options.SendAttempts);

        for (var attempt = 1; ; attempt++)
        {
            var last = attempt >= attempts;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = new ByteArrayContent(bytes);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(FhirJson.MediaType) { CharSet = "utf-8" };
                request.Headers.TryAddWithoutValidation(SignatureHeader, signature);

                using var response = await _http.SendAsync(request, ct);
                var responseBody = await ReadBodyAsync(response, ct);
                Log("POST", url, communication.TypeName, communication.Id, (int)response.StatusCode, json);

                if ((int)response.StatusCode < 500 || last)
                {
                    return new FhirSendResult(
                        (int)response.StatusCode,
                        response.IsSuccessStatusCode,
                        response.Headers.Location?.ToString(),
                        responseBody);
                }
            }
            catch (HttpRequestException) when (!last)
            {
                Log("POST", url, communication.TypeName, communication.Id, null, json);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested && !last)
            {
                Log("POST", url, communication.TypeName, communication.Id, null, json);
            }
            catch (HttpRequestException)
            {
                Log("POST", url, communication.TypeName, communication.Id, null, json);
                throw;
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                Log("POST", url, communication.TypeName, communication.Id, null, json);
                throw new HttpRequestException("CareLoop did not answer in time.", ex);
            }

            await System.Threading.Tasks.Task.Delay(TimeSpan.FromMilliseconds((long)Math.Max(0, options.RetryDelayMilliseconds) * attempt), ct);
        }
    }

    /// <summary>
    /// Reads the case <c>Task</c> for the patient case-status view with <c>GET {FhirBaseUrl}/Task/{id}</c>.
    /// </summary>
    /// <param name="taskId">Logical id of the case <c>Task</c> in CareLoop.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The parsed <c>Task</c>, or <see langword="null"/> when CareLoop returns 404.</returns>
    /// <exception cref="HttpRequestException">CareLoop could not be reached or returned an unexpected error status.</exception>
    /// <exception cref="FormatException">The response body was not a valid FHIR <c>Task</c>.</exception>
    public async System.Threading.Tasks.Task<Hl7.Fhir.Model.Task?> GetTaskAsync(string taskId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        var path = "Task/" + Uri.EscapeDataString(taskId);
        var url = new Uri(_http.BaseAddress!, path).ToString();

        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(FhirJson.MediaType));
            response = await _http.SendAsync(request, ct);
        }
        catch (HttpRequestException)
        {
            Log("GET", url, "Task", taskId, null, null);
            throw;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            Log("GET", url, "Task", taskId, null, null);
            throw new HttpRequestException("CareLoop did not answer in time.", ex);
        }

        using (response)
        {
            var body = await ReadBodyAsync(response, ct);
            Log("GET", url, "Task", taskId, (int)response.StatusCode, body);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"CareLoop answered {(int)response.StatusCode} to GET {path}.", null, response.StatusCode);
            }

            return FhirJson.Parse<Hl7.Fhir.Model.Task>(body ?? string.Empty);
        }
    }

    private static async System.Threading.Tasks.Task<string?> ReadBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        return string.IsNullOrEmpty(body) ? null : body;
    }

    private void Log(string method, string url, string resourceType, string? resourceId, int? statusCode, string? body) =>
        _store.AppendLogEntry(new FhirMessageLogEntry(
            Guid.NewGuid().ToString("N"),
            _clock.UtcNow,
            FhirMessageDirection.Outbound,
            method,
            url,
            resourceType,
            resourceId,
            statusCode,
            body));
}

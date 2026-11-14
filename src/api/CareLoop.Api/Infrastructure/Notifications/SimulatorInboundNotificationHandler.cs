using System.Security.Cryptography;
using System.Text;
using CareLoop.Api.Features.Fhir;
using CareLoop.Api.Features.Notifications;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Events;
using CareLoop.Domain.Recipients;
using Hl7.Fhir.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Infrastructure.Notifications;

/// <summary>
/// Understands what the patient-channel simulator sends back to CareLoop.
/// </summary>
/// <remarks>
/// <para>
/// The simulator plays the patient: it receives a notification and then responds as the scenario
/// under test requires (delivers or fails, acknowledges, confirms understanding, asks for a
/// call, rates their confidence, or stays silent). It reports each of those as a FHIR
/// <c>Communication</c>, which is this provider's private wire format.
/// </para>
/// <para>
/// The category, in <see cref="StatusMap.CommunicationCategorySystem"/>, and the status decide the update
/// through <see cref="StatusMap.ToNotificationUpdateKind"/>: <c>delivery-status</c> <c>completed</c> is
/// delivered and <c>not-done</c> (with its <c>statusReason</c>) is failed; <c>acknowledgement</c>,
/// <c>teach-back</c>, <c>call-me</c> and <c>confidence-rating</c> are the patient's replies, the last carrying
/// a 1 to 5 value in its first <c>payload.contentString</c> (<see cref="StatusMap.ToConfidence"/>). A delivery
/// report is never translated to an acknowledgement, and a rating is a measure, never an acknowledgement.
/// </para>
/// <para>
/// <c>about</c> must name the case <c>Task</c> (<c>Task/{caseId}</c>) that the <c>CommunicationRequest</c> named;
/// <c>basedOn</c> names that <c>CommunicationRequest</c>, whose id is the notification attempt identifier. The
/// <c>Communication</c> id and <c>meta.versionId</c> form the source event identifier, so a redelivery is
/// deduplicated. The time is <c>sent</c>, else <c>meta.lastUpdated</c>.
/// </para>
/// <para>
/// Every message must be signed: header <see cref="SignatureHeader"/> carries <c>sha256=</c> followed by the lower-case
/// hexadecimal HMAC-SHA256 of the raw body (UTF-8), keyed with <see cref="SimulatorNotificationOptions.InboundSecret"/>.
/// A missing or wrong signature, or no secret configured, is refused before the body is read, so nobody who merely
/// knows a case identifier can acknowledge for a patient.
/// </para>
/// <para>
/// A <c>Communication</c> based on a caregiver message (its <c>CommunicationRequest</c> id starts with
/// <see cref="SimulatorNotificationProvider.CaregiverIdPrefix"/>) translates to nothing: the case's notification
/// track is the patient's, and a caregiver's delivery or acknowledgement must never count as the patient's.
/// </para>
/// </remarks>
public sealed class SimulatorInboundNotificationHandler : IInboundNotificationHandler
{
    /// <summary>The header that carries the message signature.</summary>
    public const string SignatureHeader = "X-CareLoop-Signature";

    private const string SignaturePrefix = "sha256=";

    private readonly CareLoopDbContext _db;
    private readonly IOptions<SimulatorNotificationOptions> _options;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="db">Read access to cases, to find the source identity of the case a reply is about.</param>
    /// <param name="options">Supplies the secret that inbound messages are signed with.</param>
    public SimulatorInboundNotificationHandler(CareLoopDbContext db, IOptions<SimulatorNotificationOptions> options)
    {
        _db = db;
        _options = options;
    }

    /// <summary>
    /// Computes the signature header value for a body, as the patient-channel simulator must send it.
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

    /// <inheritdoc/>
    public NotificationChannel Channel => NotificationChannel.Simulator;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EventEnvelope>> TranslateAsync(InboundNotificationMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        Verify(message);
        var communication = Parse(message.Body);

        if (IsAboutCaregiverMessage(communication))
        {
            return [];
        }

        var kind = StatusMap.ToNotificationUpdateKind(communication);
        if (kind is null)
        {
            // A category or status CareLoop does not track (for example an in-progress delivery report).
            return [];
        }

        if (string.IsNullOrWhiteSpace(communication.Id))
        {
            throw new FormatException("The Communication has no id, so a redelivery could not be recognised.");
        }

        var caseId = ReferencedId(communication.About, "Task")
            ?? throw new FormatException("The Communication's about does not name the case Task.");
        var occurredAt = communication.SentElement?.ToDateTimeOffset(TimeSpan.Zero)
            ?? communication.Meta?.LastUpdated
            ?? throw new FormatException("The Communication has neither sent nor meta.lastUpdated.");

        var source = await _db.Cases.AsNoTracking()
            .Where(c => c.Id == caseId)
            .Select(c => new { c.SourceSystem, c.SourceResultId })
            .SingleOrDefaultAsync(ct)
            ?? throw new FormatException($"The Communication is about Task/{caseId:D}, which is not a known case.");

        var version = string.IsNullOrWhiteSpace(communication.Meta?.VersionId) ? "1" : communication.Meta.VersionId;
        return
        [
            new EventEnvelope
            {
                SourceSystem = source.SourceSystem,
                SourceEventId = $"Communication/{communication.Id}/_history/{version}",
                Type = CaseEventType.NotificationUpdated,
                SourceResultId = source.SourceResultId,
                OccurredAt = occurredAt,
                Actor = null,
                Payload = new NotificationUpdatePayload
                {
                    Kind = kind.Value,
                    Channel = NotificationChannel.Simulator,
                    CommunicationReference = $"Communication/{communication.Id}",
                    AttemptId = ReferencedId(communication.BasedOn, "CommunicationRequest"),
                    FailureReason = kind == NotificationUpdateKind.Failed ? FailureReason(communication) : null,
                    Confidence = kind == NotificationUpdateKind.ConfidenceRated ? StatusMap.ToConfidence(communication) : null,
                },
            },
        ];
    }

    private static Communication Parse(string body)
    {
        Resource resource;
        try
        {
            resource = FhirJsonCodec.Deserialize(body);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new FormatException($"The body is not a valid FHIR resource: {ex.Message}", ex);
        }

        return resource as Communication
            ?? throw new FormatException($"Expected a FHIR Communication, not {resource.TypeName}.");
    }

    /// <exception cref="UnauthorizedAccessException">No secret is configured, or the signature is missing or wrong.</exception>
    private void Verify(InboundNotificationMessage message)
    {
        var secret = _options.Value.InboundSecret;
        if (string.IsNullOrEmpty(secret))
        {
            throw new UnauthorizedAccessException($"{SimulatorNotificationOptions.SectionName}:InboundSecret is not configured, so no inbound message can be trusted.");
        }

        var given = message.Headers.FirstOrDefault(h => string.Equals(h.Key, SignatureHeader, StringComparison.OrdinalIgnoreCase)).Value;
        var expected = SignatureFor(message.Body, secret);
        if (string.IsNullOrEmpty(given)
            || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(given.Trim().ToLowerInvariant()), Encoding.ASCII.GetBytes(expected)))
        {
            throw new UnauthorizedAccessException("The message signature is missing or does not match.");
        }
    }

    private static bool IsAboutCaregiverMessage(Communication communication) =>
        communication.BasedOn.Any(r => r.Reference is { } value
            && value.StartsWith("CommunicationRequest/" + SimulatorNotificationProvider.CaregiverIdPrefix, StringComparison.Ordinal));

    private static Guid? ReferencedId(IEnumerable<ResourceReference> references, string type)
    {
        var prefix = type + "/";
        foreach (var reference in references)
        {
            var value = reference.Reference;
            if (value is not null && value.StartsWith(prefix, StringComparison.Ordinal) && Guid.TryParse(value[prefix.Length..], out var id))
            {
                return id;
            }
        }

        return null;
    }

    private static string FailureReason(Communication communication) =>
        communication.StatusReason?.Text
        ?? communication.StatusReason?.Coding.Select(c => c.Display ?? c.Code).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t))
        ?? "The simulator reported the message as not delivered.";
}

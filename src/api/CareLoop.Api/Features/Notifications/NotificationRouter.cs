using CareLoop.Api.Features.Events;
using CareLoop.Api.Features.Outbox;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Events;
using CareLoop.Domain.Events.DomainEvents;
using CareLoop.Domain.Recipients;
using CareLoop.Domain.Tracks;
using Microsoft.EntityFrameworkCore;

namespace CareLoop.Api.Features.Notifications;

/// <summary>
/// Outbox subscriber that sends the direct notifications a domain event calls for.
/// </summary>
/// <remarks>
/// <para>
/// Resolves the recipients with <see cref="RecipientResolver"/>, chooses the template for the event and
/// recipient kind, and sends with <see cref="NotificationSender"/>. Staff notifications carry no patient
/// detail: "a case needs your action" plus a link into the HIS (<see cref="HisLinkFor"/>).
/// </para>
/// <para>
/// For <see cref="NotificationRequested"/>, a send the provider refused is fed back to the case through
/// the event ingestor as a failed delivery, so the notification track records it and the case is flagged
/// unreachable when every channel failed. When no enabled channel can reach the patient at all, that is
/// fed back as a failure too, so a patient is never left silently unnotified. An accepted send is not
/// fed back: the track already counts as sent, and the provider reports delivery later.
/// </para>
/// <para>
/// Because deliveries are retried, a notification already sent for an event identifier is not sent again
/// (<see cref="NotificationSendLog"/>), and everything that can fail is done before anything is sent.
/// Nothing here changes an owner, a deadline or an acknowledgement.
/// </para>
/// </remarks>
public sealed class NotificationRouter : IDomainEventSubscriber
{
    /// <summary>The template every staff notification uses.</summary>
    public const string StaffTemplateKey = "staff-action-needed";

    /// <summary>Reason recorded when no enabled channel can reach the patient.</summary>
    public const string NoChannelReason = "No enabled notification channel can reach the patient.";

    private readonly RecipientResolver _recipients;
    private readonly NotificationSender _sender;
    private readonly NotificationSendLog _sent;
    private readonly EventIngestor _ingestor;
    private readonly CareLoopDbContext _db;
    private readonly ILogger<NotificationRouter> _logger;

    /// <summary>
    /// Creates the router.
    /// </summary>
    /// <param name="recipients">Resolves who to notify.</param>
    /// <param name="sender">Sends on every enabled, reachable channel.</param>
    /// <param name="sent">Remembers the events already sent for.</param>
    /// <param name="ingestor">Feeds failed patient sends back to the case.</param>
    /// <param name="db">Reads the case's source identity for the fed-back events.</param>
    /// <param name="logger">Logs refused feedback without patient detail.</param>
    public NotificationRouter(
        RecipientResolver recipients,
        NotificationSender sender,
        NotificationSendLog sent,
        EventIngestor ingestor,
        CareLoopDbContext db,
        ILogger<NotificationRouter> logger)
    {
        _recipients = recipients;
        _sender = sender;
        _sent = sent;
        _ingestor = ingestor;
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Name => "NotificationRouter";

    /// <summary>
    /// The link a staff notification carries: a reference to the case <c>Task</c>, which the HIS resolves.
    /// </summary>
    /// <param name="caseId">The case.</param>
    /// <returns>The reference, for example <c>Task/3f2b…</c>.</returns>
    public static string HisLinkFor(Guid caseId) => $"Task/{caseId:D}";

    /// <summary>
    /// The short reference a patient message quotes, so the patient can refer to it. Not a patient identifier.
    /// </summary>
    /// <param name="caseId">The case.</param>
    /// <returns>The first eight hexadecimal digits of the case identifier, upper case.</returns>
    public static string CaseReferenceFor(Guid caseId) => caseId.ToString("N")[..8].ToUpperInvariant();

    /// <inheritdoc/>
    public async Task HandleAsync(IDomainEvent domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (_sent.WasSent(domainEvent.EventId))
        {
            return;
        }

        var recipients = await _recipients.ResolveAsync(domainEvent, ct);
        if (recipients.Count == 0)
        {
            _sent.MarkSent(domainEvent.EventId);
            return;
        }

        var (templateKey, values) = TemplateFor(domainEvent);
        var eventName = domainEvent.GetType().Name;
        var attempts = new List<NotificationAttempt>();
        foreach (var recipient in recipients)
        {
            attempts.AddRange(await _sender.SendAsync(domainEvent.CaseId, recipient, templateKey, eventName, values, ct, domainEvent.EventId));
        }

        // From here on something may have been sent, so nothing below may make the outbox retry.
        _sent.MarkSent(domainEvent.EventId);

        if (domainEvent is NotificationRequested requested)
        {
            await FeedBackFailuresAsync(requested, recipients[0], attempts, ct);
        }
    }

    private static (string TemplateKey, IReadOnlyDictionary<string, string> Values) TemplateFor(IDomainEvent domainEvent) =>
        domainEvent is NotificationRequested requested
            ? (requested.TemplateKey, new Dictionary<string, string> { ["caseReference"] = CaseReferenceFor(requested.CaseId) })
            : (StaffTemplateKey, new Dictionary<string, string> { ["hisLink"] = HisLinkFor(domainEvent.CaseId) });

    private async Task FeedBackFailuresAsync(NotificationRequested requested, Recipient patient, IReadOnlyList<NotificationAttempt> attempts, CancellationToken ct)
    {
        var failures = attempts.Where(a => a.Status == NotificationAttemptStatus.Failed).ToList();
        if (attempts.Count == 0)
        {
            var channel = patient.ContactPoints.Select(c => c.Channel).DefaultIfEmpty(NotificationChannel.Simulator).First();
            failures.Add(new NotificationAttempt
            {
                Id = NotificationSender.AttemptIdFor(requested.EventId, patient.Reference, channel),
                Channel = channel,
                AttemptedAt = requested.OccurredAt,
                Status = NotificationAttemptStatus.Failed,
                UsedSmsSafeTemplate = false,
                FailureReason = NoChannelReason,
            });
        }

        if (failures.Count == 0)
        {
            return;
        }

        try
        {
            var source = await _db.Cases.AsNoTracking()
                .Where(c => c.Id == requested.CaseId)
                .Select(c => new { c.SourceSystem, c.SourceResultId })
                .SingleOrDefaultAsync(ct);
            if (source is null)
            {
                _logger.LogWarning("Case {CaseId} for {EventId} no longer exists; failed sends were not recorded", requested.CaseId, requested.EventId);
                return;
            }

            foreach (var failure in failures)
            {
                var result = await _ingestor.IngestAsync(new EventEnvelope
                {
                    SourceSystem = source.SourceSystem,
                    SourceEventId = $"notification-attempt/{failure.Id:N}/failed",
                    Type = CaseEventType.NotificationUpdated,
                    SourceResultId = source.SourceResultId,
                    OccurredAt = failure.AttemptedAt,
                    Actor = null,
                    Payload = new NotificationUpdatePayload
                    {
                        Kind = NotificationUpdateKind.Failed,
                        Channel = failure.Channel,
                        CommunicationReference = $"notification-attempt/{failure.Id:D}",
                        AttemptId = failure.Id,
                        FailureReason = failure.FailureReason,
                    },
                }, ct);

                if (result.Outcome is IngestOutcome.Invalid or IngestOutcome.Rejected)
                {
                    _logger.LogWarning(
                        "Failed send {AttemptId} on case {CaseId} was not recorded ({Outcome}): {Reason}",
                        failure.Id, requested.CaseId, result.Outcome, result.Reason);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Retrying would send again, so a failure to record is logged rather than thrown.
            _logger.LogError(ex, "Failed sends for {EventId} on case {CaseId} could not be recorded", requested.EventId, requested.CaseId);
        }
    }
}

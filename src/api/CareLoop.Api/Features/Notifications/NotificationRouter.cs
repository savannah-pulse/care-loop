using System.Globalization;
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
/// detail: "a case needs your action" plus its case number (<see cref="CaseReferencePlaceholder"/>), with no link; an escalation
/// notice also says who acknowledged the previous level, if anyone (<see cref="PreviousAcknowledgementFor"/>).
/// </para>
/// <para>
/// <see cref="AppointmentScheduled"/> goes to a consented caregiver only, with the appointment's date, time and
/// place and nothing else (<see cref="CaregiverTemplateKey"/>, or <see cref="CaregiverPlaceToBeConfirmedTemplateKey"/>
/// when the source named no place). Its attempts are not fed into the case's notification track, which follows the
/// patient's notification: a caregiver message is never the patient's acknowledgement or delivery.
/// </para>
/// <para>
/// For <see cref="NotificationRequested"/>, every channel that can reach the patient is recorded on the case's
/// notification track as sent, through the event ingestor, before anything is sent. A delivery report that comes
/// back while the next channel is still sending is then judged against every channel, so one channel's failure
/// never marks the patient unreachable while another message is on its way. Each channel a provider refused is
/// then fed back as a failure, and when no enabled channel can reach the patient at all, that is fed back too, so
/// the case is flagged unreachable: a patient is never left silently unnotified. If recording fails, the exception
/// propagates so the outbox retries it.
/// </para>
/// <para>
/// For staff and caregiver messages there is no case track to record a failure on, so a refused send, or a
/// recipient no enabled channel can reach, makes <see cref="HandleAsync"/> throw and the outbox retry (and in the
/// end park) the delivery. Every attempt a provider accepted is remembered (<see cref="NotificationSendLog"/>), so a
/// retry resends only the channels that failed. Everything that can fail before sending is done before anything is
/// sent. Nothing here changes an owner, a deadline or an acknowledgement.
/// </para>
/// </remarks>
public sealed class NotificationRouter : IDomainEventSubscriber
{
    /// <summary>The template every staff notification uses.</summary>
    public const string StaffTemplateKey = "staff-action-needed";

    /// <summary>The template of the caregiver's appointment message.</summary>
    public const string CaregiverTemplateKey = "caregiver-appointment";

    /// <summary>The caregiver's appointment message when the source named no place; it says the place will be confirmed.</summary>
    public const string CaregiverPlaceToBeConfirmedTemplateKey = "caregiver-appointment-place-tbc";

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
    /// The placeholder a staff template names the case with: its case number (<see cref="CaseReferenceFor"/>), as the
    /// dashboard shows it. Not a patient identifier, and not the hospital system's own result identifier, which is.
    /// </summary>
    public const string CaseReferencePlaceholder = "caseReference";

    /// <summary>
    /// The short reference a patient message quotes, so the patient can refer to it. Not a patient identifier.
    /// </summary>
    /// <param name="caseId">The case.</param>
    /// <returns>The first eight hexadecimal digits of the case identifier, upper case.</returns>
    public static string CaseReferenceFor(Guid caseId) => caseId.ToString("N")[..8].ToUpperInvariant();

    /// <summary>
    /// The line an escalation notice carries about the previous level's acknowledgement.
    /// </summary>
    /// <param name="escalation">The escalation being announced.</param>
    /// <returns>
    /// For example <c>Level 1 was acknowledged by facility-lead at 09:40 UTC on 2026-11-14.</c>, or an empty string
    /// when nobody acknowledged the previous level (always at level 1). Only the role is named, never a person.
    /// </returns>
    public static string PreviousAcknowledgementFor(EscalationRaised escalation)
    {
        ArgumentNullException.ThrowIfNull(escalation);
        if (string.IsNullOrWhiteSpace(escalation.AcknowledgedByRole) || escalation.AcknowledgedAt is not { } at)
        {
            return string.Empty;
        }

        var utc = at.ToUniversalTime();
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Level {escalation.Level - 1} was acknowledged by {escalation.AcknowledgedByRole} at {utc:HH:mm} UTC on {utc:yyyy-MM-dd}.");
    }

    /// <inheritdoc/>
    /// <exception cref="NotificationNotSentException">A staff or caregiver message was refused on a channel, or no enabled channel can reach its recipient; the outbox retries.</exception>
    public async Task HandleAsync(IDomainEvent domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var recipients = await _recipients.ResolveAsync(domainEvent, ct);
        if (recipients.Count == 0)
        {
            return;
        }

        var (templateKey, values) = TemplateFor(domainEvent);

        var eventName = domainEvent.GetType().Name;
        var attemptsByRecipient = new List<(Recipient Recipient, IReadOnlyList<NotificationAttempt> Attempts)>();
        if (domainEvent is NotificationRequested toRecord)
        {
            // Before anything is sent, so a delivery report that comes back at once is judged against every channel.
            await RecordSendsAsync(toRecord, recipients[0], eventName, ct);
        }

        foreach (var recipient in recipients)
        {
            var attempts = await _sender.SendAsync(domainEvent.CaseId, recipient, templateKey, eventName, values, ct, domainEvent.EventId, _sent.WasSent);
            foreach (var accepted in attempts.Where(a => a.Status == NotificationAttemptStatus.Sent))
            {
                _sent.MarkSent(accepted.Id);
            }

            attemptsByRecipient.Add((recipient, attempts));
        }

        if (domainEvent is NotificationRequested requested)
        {
            var (patient, attempts) = attemptsByRecipient[0];
            await FeedBackFailuresAsync(requested, patient, attempts, ct);
            return;
        }

        var notSent = attemptsByRecipient
            .Where(r => r.Attempts.Count == 0 || r.Attempts.Any(a => a.Status == NotificationAttemptStatus.Failed))
            .ToList();
        if (notSent.Count > 0)
        {
            // Only the recipient kind, channel and provider reason: never a patient detail in an exception the outbox stores.
            var reasons = notSent.SelectMany(r => r.Attempts.Count == 0
                ? [$"{r.Recipient.Kind}: no enabled channel can reach the recipient"]
                : r.Attempts.Where(a => a.Status == NotificationAttemptStatus.Failed).Select(a => $"{r.Recipient.Kind} on {a.Channel}: {a.FailureReason}"));
            throw new NotificationNotSentException($"{eventName} {domainEvent.EventId} was not sent to every recipient: {string.Join("; ", reasons)}");
        }
    }

    private static (string TemplateKey, IReadOnlyDictionary<string, string> Values) TemplateFor(IDomainEvent domainEvent) =>
        domainEvent switch
        {
            NotificationRequested requested =>
                (requested.TemplateKey, new Dictionary<string, string> { ["caseReference"] = CaseReferenceFor(requested.CaseId) }),
            AppointmentScheduled scheduled => CaregiverTemplateFor(scheduled),
            _ => (StaffTemplateKey, new Dictionary<string, string>
            {
                [CaseReferencePlaceholder] = CaseReferenceFor(domainEvent.CaseId),
                ["previousAcknowledgement"] = domainEvent is EscalationRaised escalation ? PreviousAcknowledgementFor(escalation) : string.Empty,
            }),
        };

    private static (string TemplateKey, IReadOnlyDictionary<string, string> Values) CaregiverTemplateFor(AppointmentScheduled scheduled)
    {
        // The time as the hospital system gave it, in a language-neutral form; never anything about the result.
        var values = new Dictionary<string, string>
        {
            ["appointmentDate"] = scheduled.ScheduledFor.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["appointmentTime"] = scheduled.ScheduledFor.ToString("HH:mm", CultureInfo.InvariantCulture),
        };
        if (string.IsNullOrWhiteSpace(scheduled.Location))
        {
            return (CaregiverPlaceToBeConfirmedTemplateKey, values);
        }

        values["place"] = scheduled.Location.Trim();
        return (CaregiverTemplateKey, values);
    }

    private async Task FeedBackFailuresAsync(NotificationRequested requested, Recipient patient, IReadOnlyList<NotificationAttempt> attempts, CancellationToken ct)
    {
        // Every channel was recorded as sent beforehand, so a refusal on one channel counts as a failure only once
        // every other channel has failed too: each refusal can be recorded as it is.
        var failures = attempts.Where(a => a.Status == NotificationAttemptStatus.Failed).ToList();
        if (attempts.Count == 0)
        {
            // An attempt needs a channel. With no contact point at all, the text message is the one that was missed.
            var channel = patient.ContactPoints.Select(c => c.Channel).DefaultIfEmpty(NotificationChannel.Sms).First();
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

        foreach (var failure in failures)
        {
            await RecordOnTrackAsync(requested, failure.Id, failure.Channel, NotificationUpdateKind.Failed, failure.AttemptedAt, failure.FailureReason, ct);
        }
    }

    private async Task RecordSendsAsync(NotificationRequested requested, Recipient patient, string eventName, CancellationToken ct)
    {
        foreach (var channel in _sender.ReachableChannels(patient, eventName))
        {
            var attemptId = NotificationSender.AttemptIdFor(requested.EventId, patient.Reference, channel);
            await RecordOnTrackAsync(requested, attemptId, channel, NotificationUpdateKind.Sent, requested.OccurredAt, null, ct);
        }
    }

    private async Task RecordOnTrackAsync(NotificationRequested requested, Guid attemptId, NotificationChannel channel,
        NotificationUpdateKind kind, DateTimeOffset at, string? failureReason, CancellationToken ct)
    {
        var source = await _db.Cases.AsNoTracking()
            .Where(c => c.Id == requested.CaseId)
            .Select(c => new { c.SourceSystem, c.SourceResultId })
            .SingleOrDefaultAsync(ct);
        if (source is null)
        {
            _logger.LogWarning("Case {CaseId} for {EventId} no longer exists; the {Kind} attempt was not recorded", requested.CaseId, requested.EventId, kind);
            return;
        }

        var result = await _ingestor.IngestAsync(new EventEnvelope
        {
            SourceSystem = source.SourceSystem,
            SourceEventId = $"notification-attempt/{attemptId:N}/{kind.ToString().ToLowerInvariant()}",
            Type = CaseEventType.NotificationUpdated,
            SourceResultId = source.SourceResultId,
            OccurredAt = at,
            Actor = null,
            Payload = new NotificationUpdatePayload
            {
                Kind = kind,
                Channel = channel,
                CommunicationReference = $"notification-attempt/{attemptId:D}",
                AttemptId = attemptId,
                FailureReason = failureReason,
            },
        }, ct);

        // A rule refusing it will refuse it again, so that is logged; anything thrown (the database, a
        // concurrency conflict) propagates and the outbox retries. Accepted attempts are not resent on retry.
        if (result.Outcome is IngestOutcome.Invalid or IngestOutcome.Rejected)
        {
            _logger.LogWarning(
                "{Kind} attempt {AttemptId} on case {CaseId} was not recorded ({Outcome}): {Reason}",
                kind, attemptId, requested.CaseId, result.Outcome, result.Reason);
        }
    }
}

/// <summary>
/// A staff or caregiver notification was not sent on every channel, so the outbox must retry it.
/// </summary>
/// <remarks>Its message names recipient kinds, channels and provider reasons only, never a patient detail, because the outbox stores it.</remarks>
public sealed class NotificationNotSentException : Exception
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="message">What was not sent and why.</param>
    public NotificationNotSentException(string message)
        : base(message)
    {
    }
}

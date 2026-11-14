using System.Text.Json;
using System.Text.Json.Serialization;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Events.DomainEvents;
using CareLoop.Domain.Recipients;
using Microsoft.EntityFrameworkCore;

namespace CareLoop.Api.Features.Notifications;

/// <summary>
/// Works out who must be notified directly about a domain event, and how they can be reached.
/// </summary>
/// <remarks>
/// Applies the routing table: <see cref="NotificationRequested"/> to the patient;
/// <see cref="HandoffProposed"/> to the proposed owner; <see cref="HandoffRejected"/>,
/// <see cref="ReferralRejected"/>, <see cref="CallRequested"/> and <see cref="DeadlineApproaching"/> to the
/// current owner; <see cref="EscalationRaised"/> to the step of the escalation ladder it reached (the facility
/// lead, then the coordinator); <see cref="AppointmentScheduled"/> to the caregiver while the case's caregiver
/// consent is active, and to nobody else; <see cref="CaseClosed"/> and <see cref="HandoffSummaryApproved"/> to nobody.
/// The Domain holds no contact data, so patient contact points are read from the stored screening-result
/// event, the caregiver's from the case's <see cref="Consent"/>, and staff are addressed through their simulator inbox.
/// Consent is read when the message is about to be sent, not when the event was raised, because the outbox
/// may deliver after a withdrawal.
/// </remarks>
public sealed class RecipientResolver
{
    /// <summary>The language of staff notifications. Staff templates are English only for now.</summary>
    public const string StaffLanguage = "en";

    private static readonly JsonSerializerOptions PayloadSerializerOptions = CreatePayloadSerializerOptions();

    private readonly CareLoopDbContext _db;

    /// <summary>
    /// Creates the resolver.
    /// </summary>
    /// <param name="db">Read access to stored source events, used to find patient contact points.</param>
    public RecipientResolver(CareLoopDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Resolves the recipients of a domain event.
    /// </summary>
    /// <param name="domainEvent">The event to route.</param>
    /// <param name="ct">Cancels the operation.</param>
    /// <returns>The recipients to notify; empty when the event has no direct notification.</returns>
    /// <exception cref="InvalidOperationException">A patient notification was requested but the case has no stored screening result to read contact points from.</exception>
    public async Task<IReadOnlyList<Recipient>> ResolveAsync(IDomainEvent domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return domainEvent switch
        {
            NotificationRequested requested => [await PatientAsync(requested, ct)],
            AppointmentScheduled scheduled => await CaregiverAsync(scheduled, ct),
            HandoffProposed proposed => [ForOwner(proposed.ProposedOwner)],
            HandoffRejected rejected => [ForOwner(rejected.CurrentOwner)],
            ReferralRejected rejected => [ForOwner(rejected.CurrentOwner)],
            CallRequested call => [ForOwner(call.CurrentOwner)],
            DeadlineApproaching approaching => [ForOwner(approaching.CurrentOwner)],
            EscalationRaised escalation => [ForOwner(escalation.EscalatedTo)],
            _ => [],
        };
    }

    /// <summary>
    /// Builds the staff recipient for a case owner.
    /// </summary>
    /// <param name="owner">The owner, role or queue to address.</param>
    /// <returns>A staff recipient reachable through the HIS simulator's staff inbox.</returns>
    public Recipient ForOwner(CaseOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return new Recipient
        {
            Reference = owner.Reference,
            Kind = RecipientKind.Staff,
            Language = StaffLanguage,
            ContactPoints = [new ContactPoint { Channel = NotificationChannel.Simulator, Address = owner.Reference }],
        };
    }

    private async Task<Recipient> PatientAsync(NotificationRequested requested, CancellationToken ct)
    {
        var payload = await ScreeningResultAsync(requested.CaseId, ct)
            ?? throw new InvalidOperationException($"Case {requested.CaseId} has no stored screening result, so the patient cannot be reached.");

        return new Recipient
        {
            Reference = requested.PatientReference,
            Kind = RecipientKind.Patient,
            Language = payload.PatientLanguage,
            ContactPoints = payload.PatientContactPoints,
        };
    }

    private async Task<IReadOnlyList<Recipient>> CaregiverAsync(AppointmentScheduled scheduled, CancellationToken ct)
    {
        var @case = await _db.Cases.AsNoTracking().SingleOrDefaultAsync(c => c.Id == scheduled.CaseId, ct);
        if (@case?.CaregiverConsent is not { IsActive: true } consent)
        {
            return [];
        }

        // The caregiver's own language is not recorded; the patient's is the best available guess, and the
        // renderer falls back to English when no template exists in it.
        var language = (await ScreeningResultAsync(scheduled.CaseId, ct))?.PatientLanguage;
        return
        [
            new Recipient
            {
                Reference = consent.EvidenceReference,
                Kind = RecipientKind.Caregiver,
                Language = string.IsNullOrWhiteSpace(language) ? TemplateRenderer.FallbackLanguage : language,
                ContactPoints = [consent.CaregiverContactPoint],
            },
        ];
    }

    /// <summary>The latest applied screening result, the current source of the patient's language and contact points.</summary>
    private async Task<ScreeningResultPayload?> ScreeningResultAsync(Guid caseId, CancellationToken ct)
    {
        var stored = await _db.SourceEvents.AsNoTracking()
            .Where(e => e.CaseId == caseId
                && e.Type == CaseEventType.ScreeningResultReceived
                && e.Disposition == SourceEventDisposition.Applied)
            .Select(e => new { e.PayloadJson, e.ReceivedAt })
            .ToListAsync(ct);
        var latest = stored.OrderByDescending(e => e.ReceivedAt).FirstOrDefault();
        if (latest is null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<ScreeningResultPayload>(latest.PayloadJson, PayloadSerializerOptions)
            ?? throw new InvalidOperationException($"The screening result of case {caseId} has an empty payload.");
    }

    /// <summary>Must match the options <c>EventIngestor</c> stores payloads with: web defaults, enums as names.</summary>
    private static JsonSerializerOptions CreatePayloadSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

using CareLoop.Api.Features.Outbox;
using CareLoop.Api.Infrastructure.Fhir;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Domain.Events.DomainEvents;
using Microsoft.EntityFrameworkCore;

namespace CareLoop.Api.Features.Fhir.Outbound;

/// <summary>
/// Outbox subscriber that publishes domain events to external systems as FHIR resources.
/// </summary>
/// <remarks>
/// Sends: <see cref="HandoffProposed"/> as the case <c>Task</c>; <see cref="HandoffSummaryApproved"/> as a
/// <c>DocumentReference</c> carrying exactly the summary that approval names; <see cref="HandoffRejected"/> as the case <c>Task</c> with status
/// <c>rejected</c> and the reason given (unless a newer handoff is already pending); <see cref="ReferralRejected"/> as a
/// case <c>Task</c> update; <see cref="CallRequested"/> and <see cref="EscalationRaised"/> as a notice <c>Task</c>;
/// <see cref="CaseClosed"/> as the case <c>Task</c> completed. <see cref="NotificationRequested"/> and
/// <see cref="DeadlineApproaching"/> publish nothing: reaching the patient is the notification providers' job, not the
/// FHIR boundary's. Resources use deterministic identifiers (the case identifier for the case task, the event or
/// summary identifier otherwise) so a retried delivery is idempotent.
/// <para>
/// The case <c>Task</c> is state, not an event: it is built from the case as it stands when the message is delivered,
/// so a late delivery sends the latest state rather than a stale one.
/// </para>
/// <para>
/// Routing: every message is about a case and goes to the system the case came from (its
/// source system identifier). A message whose system is not configured fails and is retried
/// by the outbox; it is never sent elsewhere.
/// </para>
/// </remarks>
public sealed class FhirPublisher : IDomainEventSubscriber
{
    private readonly FhirHttpClient _client;
    private readonly EscalationTaskMapper _escalationTasks;
    private readonly HandoffDocumentReferenceMapper _handoffDocuments;
    private readonly CaseTaskMapper _caseTasks;
    private readonly CareLoopDbContext _db;

    /// <summary>
    /// Creates the publisher.
    /// </summary>
    /// <param name="client">Sends resources to the external systems.</param>
    /// <param name="escalationTasks">Builds escalation and call-request tasks.</param>
    /// <param name="handoffDocuments">Builds handoff summary documents.</param>
    /// <param name="caseTasks">Builds the case status task.</param>
    /// <param name="db">Reads the case and its approved summary, with no-tracking queries.</param>
    public FhirPublisher(
        FhirHttpClient client,
        EscalationTaskMapper escalationTasks,
        HandoffDocumentReferenceMapper handoffDocuments,
        CaseTaskMapper caseTasks,
        CareLoopDbContext db)
    {
        _client = client;
        _escalationTasks = escalationTasks;
        _handoffDocuments = handoffDocuments;
        _caseTasks = caseTasks;
        _db = db;
    }

    /// <inheritdoc/>
    public string Name => "FhirPublisher";

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The case does not exist, or its source system is not configured; the outbox retries.</exception>
    public async Task HandleAsync(IDomainEvent domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (domainEvent is NotificationRequested or DeadlineApproaching)
        {
            return;
        }

        var @case = await _db.Cases.AsNoTracking().SingleOrDefaultAsync(c => c.Id == domainEvent.CaseId, ct)
            ?? throw new InvalidOperationException($"Case {domainEvent.CaseId} for {domainEvent.GetType().Name} {domainEvent.EventId} does not exist.");

        var system = @case.SourceSystem;

        switch (domainEvent)
        {
            case HandoffSummaryApproved approval:
                var summary = await _db.HandoffSummaries.AsNoTracking()
                    .SingleOrDefaultAsync(s => s.Id == approval.SummaryId && s.CaseId == approval.CaseId, ct)
                    ?? throw new InvalidOperationException($"Handoff summary {approval.SummaryId} named by {approval.EventId} does not exist on case {approval.CaseId}.");
                await _client.SendAsync(system, _handoffDocuments.Map(approval, summary), ct);
                break;

            case HandoffRejected rejection:
                await _client.SendAsync(system, _caseTasks.Map(@case, rejection), ct);
                break;

            case HandoffProposed or ReferralRejected or CaseClosed:
                await _client.SendAsync(system, _caseTasks.Map(@case), ct);
                break;

            case CallRequested call:
                await _client.SendAsync(
                    system,
                    _escalationTasks.Map(call.EventId, call.CaseId, call.CurrentOwner, StatusMap.CallRequestTaskCode, "The patient asked to be called.", call.OccurredAt),
                    ct);
                break;

            case EscalationRaised escalation:
                await _client.SendAsync(
                    system,
                    _escalationTasks.Map(escalation.EventId, escalation.CaseId, escalation.EscalatedTo, StatusMap.EscalationTaskCode, $"Escalation level {escalation.Level}: {escalation.Reason}", escalation.OccurredAt),
                    ct);
                break;
        }
    }
}

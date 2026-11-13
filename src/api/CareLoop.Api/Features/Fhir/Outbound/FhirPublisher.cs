using CareLoop.Api.Features.Outbox;
using CareLoop.Api.Infrastructure.Fhir;
using CareLoop.Domain.Events.DomainEvents;

namespace CareLoop.Api.Features.Fhir.Outbound;

/// <summary>
/// Outbox subscriber that publishes domain events to external systems as FHIR resources.
/// </summary>
/// <remarks>
/// Will send: <see cref="HandoffProposed"/> as a <c>Task</c> plus a <c>DocumentReference</c> (only when an approved
/// summary exists); <see cref="HandoffRejected"/> and <see cref="ReferralRejected"/> as a <c>Task</c> update;
/// <see cref="CallRequested"/> and <see cref="EscalationRaised"/> as a <c>Task</c>; <see cref="CaseClosed"/> as the
/// case <c>Task</c> completed. <see cref="NotificationRequested"/> and <see cref="DeadlineApproaching"/> publish
/// nothing: reaching the patient is the notification providers' job, not the FHIR boundary's. Resources use
/// deterministic identifiers derived from the event identifier so a retried delivery is idempotent.
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

    /// <summary>
    /// Creates the publisher.
    /// </summary>
    /// <param name="client">Sends resources to the external systems.</param>
    /// <param name="escalationTasks">Builds escalation and call-request tasks.</param>
    /// <param name="handoffDocuments">Builds handoff summary documents.</param>
    /// <param name="caseTasks">Builds the case status task.</param>
    public FhirPublisher(
        FhirHttpClient client,
        EscalationTaskMapper escalationTasks,
        HandoffDocumentReferenceMapper handoffDocuments,
        CaseTaskMapper caseTasks)
    {
        _client = client;
        _escalationTasks = escalationTasks;
        _handoffDocuments = handoffDocuments;
        _caseTasks = caseTasks;
    }

    /// <inheritdoc/>
    public string Name => "FhirPublisher";

    /// <inheritdoc/>
    public Task HandleAsync(IDomainEvent domainEvent, CancellationToken ct)
    {
        // TODO(WS-3): switch on the event type, map with the outbound mappers and send with FhirHttpClient.
        throw new NotImplementedException("WS3: FhirPublisher.HandleAsync");
    }
}

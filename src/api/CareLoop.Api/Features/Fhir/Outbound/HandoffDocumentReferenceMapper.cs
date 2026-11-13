using CareLoop.Domain.Cases;
using CareLoop.Domain.Events.DomainEvents;
using CareLoop.Domain.Recipients;
using Hl7.Fhir.Model;
using FhirTask = Hl7.Fhir.Model.Task;

namespace CareLoop.Api.Features.Fhir.Outbound;

/// <summary>
/// Builds the <c>DocumentReference</c> carrying an approved handoff summary.
/// </summary>
/// <remarks>
/// Will attach only a summary a person has approved; an AI draft is never published. The document references the case <c>Task</c> and names the approver as author.
/// </remarks>
public sealed class HandoffDocumentReferenceMapper
{
    /// <summary>
    /// Builds the FHIR resource.
    /// </summary>
    /// <param name="domainEvent">The handoff proposal the summary accompanies.</param>
    /// <param name="approvedSummaryText">The summary text exactly as approved.</param>
    /// <param name="approvedByReference">Reference of the person who approved the summary.</param>
    /// <returns>A <c>DocumentReference</c> ready to send to the HIS.</returns>
    public DocumentReference Map(HandoffProposed domainEvent, string approvedSummaryText, string approvedByReference)
    {
        // TODO(WS-3): populate content, context and author.
        throw new NotImplementedException("WS3: HandoffDocumentReferenceMapper.Map");
    }
}

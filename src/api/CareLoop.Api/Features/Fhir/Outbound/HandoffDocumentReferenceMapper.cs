using System.Text;
using CareLoop.Api.Infrastructure.Persistence.Summaries;
using CareLoop.Contracts.Summaries;
using CareLoop.Domain.Events.DomainEvents;
using Hl7.Fhir.Model;

namespace CareLoop.Api.Features.Fhir.Outbound;

/// <summary>
/// Builds the <c>DocumentReference</c> carrying an approved handoff summary.
/// </summary>
/// <remarks>
/// Attaches only a summary a person has approved; an AI draft or a rejected summary is refused here, so no caller can
/// publish one by mistake. The document references the case <c>Task</c>, names the approver as author and
/// authenticator, and carries the approved text exactly as approved. Its identifier is derived from the summary, so a
/// retried send overwrites the same document.
/// </remarks>
public sealed class HandoffDocumentReferenceMapper
{
    /// <summary>
    /// Builds the FHIR resource.
    /// </summary>
    /// <param name="domainEvent">The handoff proposal the summary accompanies.</param>
    /// <param name="summary">The stored summary; must be approved.</param>
    /// <returns>A <c>DocumentReference</c> ready to send to the HIS.</returns>
    /// <exception cref="InvalidOperationException">The summary is not approved, or has no approver.</exception>
    public DocumentReference Map(HandoffProposed domainEvent, HandoffSummaryRecord summary)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        ArgumentNullException.ThrowIfNull(summary);

        if (summary.Status != HandoffSummaryStatus.Approved || string.IsNullOrWhiteSpace(summary.ApprovedBy))
        {
            throw new InvalidOperationException($"Handoff summary {summary.Id} is {summary.Status}; only an approved summary is ever published.");
        }

        var approver = new ResourceReference(OutboundSupport.Typed("Practitioner", summary.ApprovedBy));

        return new DocumentReference
        {
            Id = $"handoff-{summary.Id:N}",
            Status = DocumentReferenceStatus.Current,
            DocStatus = CompositionStatus.Final,
            Type = new CodeableConcept { Text = "Handoff summary" },
            Date = summary.ApprovedAt ?? domainEvent.OccurredAt,
            Author = [approver],
            Authenticator = approver,
            Description = "Handoff summary approved by a person; AI-assisted draft.",
            Content =
            [
                new DocumentReference.ContentComponent
                {
                    Attachment = new Attachment
                    {
                        ContentType = "text/plain; charset=utf-8",
                        Language = summary.Language,
                        Data = Encoding.UTF8.GetBytes(summary.Text),
                        Title = "Handoff summary",
                    },
                },
            ],
            Context = new DocumentReference.ContextComponent
            {
                Related = [new ResourceReference($"Task/{OutboundSupport.CaseTaskId(domainEvent.CaseId)}")],
            },
        };
    }
}

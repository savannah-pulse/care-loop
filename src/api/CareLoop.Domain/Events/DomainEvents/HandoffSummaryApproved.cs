namespace CareLoop.Domain.Events.DomainEvents;

/// <summary>
/// A person approved a handoff summary, so its text may now be sent to the hospital system.
/// </summary>
/// <remarks>
/// Published as a FHIR <c>DocumentReference</c> carrying exactly the approved text. No direct notification is sent.
/// <para>
/// The summary is published on approval, not when a handoff is proposed, because a draft is usually made when
/// the handoff is proposed and approved later. Naming the summary also stops an older approved summary from an
/// earlier handoff being sent in its place. AI output stays a draft until a person approves it, so this is the
/// only event that lets a summary leave CareLoop.
/// </para>
/// </remarks>
public sealed record HandoffSummaryApproved : IDomainEvent
{
    /// <inheritdoc/>
    public required Guid EventId { get; init; }

    /// <inheritdoc/>
    public required Guid CaseId { get; init; }

    /// <inheritdoc/>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>Identifier of the approved summary record; the publisher sends this one and no other.</summary>
    public required Guid SummaryId { get; init; }

    /// <summary>The person who approved the text and took responsibility for it.</summary>
    public required Actor ApprovedBy { get; init; }
}

using CareLoop.Domain.Cases;
using CareLoop.Domain.Recipients;

namespace CareLoop.Domain.Events.DomainEvents;

/// <summary>
/// The patient must be told about their result or next step.
/// </summary>
/// <remarks>
/// Sent to the patient on every enabled notification channel. Sending it does not complete the notification track: only the patient's acknowledgement does.
/// </remarks>
public sealed record NotificationRequested : IDomainEvent
{
    /// <inheritdoc/>
    public required Guid EventId { get; init; }

    /// <inheritdoc/>
    public required Guid CaseId { get; init; }

    /// <inheritdoc/>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>Source-system reference of the patient to notify. Contact points are resolved outside the Domain, which holds no contact data.</summary>
    public required string PatientReference { get; init; }

    /// <summary>Key of the approved message template to render.</summary>
    public required string TemplateKey { get; init; }
}

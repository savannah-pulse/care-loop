using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Recipients;
using CareLoop.Domain.Tracks;

namespace CareLoop.Contracts.Events.Payloads;

/// <summary>
/// Payload of <see cref="CaseEventType.NotificationUpdated"/>: a delivery report or a response from the patient.
/// </summary>
public sealed record NotificationUpdatePayload : EventPayload
{
    /// <summary>Whether this is a delivery, a failure, an acknowledgement, a teach-back, a call-me request or a confidence rating.</summary>
    public required NotificationUpdateKind Kind { get; init; }

    /// <summary>The channel the report or response came from.</summary>
    public required NotificationChannel Channel { get; init; }

    /// <summary>Reference to the source <c>Communication</c>.</summary>
    public required string CommunicationReference { get; init; }

    /// <summary>The notification attempt the report refers to, when the source identified it.</summary>
    public Guid? AttemptId { get; init; }

    /// <summary>The failure reason, when <see cref="Kind"/> is <see cref="NotificationUpdateKind.Failed"/>.</summary>
    public string? FailureReason { get; init; }

    /// <summary>The patient's rating from 1 to 5, when <see cref="Kind"/> is <see cref="NotificationUpdateKind.ConfidenceRated"/>; otherwise <see langword="null"/>.</summary>
    public int? Confidence { get; init; }
}

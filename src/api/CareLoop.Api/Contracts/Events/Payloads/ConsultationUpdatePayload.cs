using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Recipients;
using CareLoop.Domain.Tracks;

namespace CareLoop.Contracts.Events.Payloads;

/// <summary>
/// Payload of <see cref="CaseEventType.ConsultationUpdated"/>: the consultation, and the handoff it carries, changed status.
/// </summary>
public sealed record ConsultationUpdatePayload : EventPayload
{
    /// <summary>The new status of the consultation.</summary>
    public required ConsultationStatus Status { get; init; }

    /// <summary>Reference to the consultation <c>ServiceRequest</c>.</summary>
    public required string ServiceRequestReference { get; init; }

    /// <summary>Reference to the consultation <c>Task</c>.</summary>
    public required string TaskReference { get; init; }

    /// <summary>The owner the case is proposed to, when <see cref="Status"/> is <see cref="ConsultationStatus.OwnerProposed"/>.</summary>
    public CaseOwner? ProposedOwner { get; init; }

    /// <summary>The reason given, when the consultation was rejected.</summary>
    public string? Reason { get; init; }
}

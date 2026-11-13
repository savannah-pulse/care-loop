using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Recipients;
using CareLoop.Domain.Tracks;

namespace CareLoop.Contracts.Events.Payloads;

/// <summary>
/// Payload of <see cref="CaseEventType.ReferralUpdated"/>: the referral changed status.
/// </summary>
public sealed record ReferralUpdatePayload : EventPayload
{
    /// <summary>The new status of the referral.</summary>
    public required ReferralStatus Status { get; init; }

    /// <summary>Reference to the referral <c>ServiceRequest</c>.</summary>
    public required string ServiceRequestReference { get; init; }

    /// <summary>Reference to the referral <c>Task</c>.</summary>
    public required string TaskReference { get; init; }

    /// <summary>The facility the patient is referred to.</summary>
    public string? ReceivingFacilityId { get; init; }

    /// <summary>The reason given, when the referral was rejected.</summary>
    public string? Reason { get; init; }
}

using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Recipients;
using CareLoop.Domain.Tracks;

namespace CareLoop.Contracts.Events.Payloads;

/// <summary>
/// Payload of <see cref="CaseEventType.ClinicalReviewCompleted"/>: a clinician completed the review of the result.
/// </summary>
public sealed record ClinicalReviewPayload : EventPayload
{
    /// <summary>The urgency the clinician set, taken from <c>Task.priority</c>. CareLoop stores it and never changes it.</summary>
    public required string UrgencyCode { get; init; }

    /// <summary>Reference to the completed review <c>Task</c>.</summary>
    public required string ReviewTaskReference { get; init; }
}

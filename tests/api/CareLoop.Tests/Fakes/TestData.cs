using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;

namespace CareLoop.Tests.Fakes;

/// <summary>
/// Builders for synthetic test data. Every identifier is prefixed <c>SYN-</c>.
/// </summary>
public static class TestData
{
    /// <summary>The synthetic source system used in tests.</summary>
    public const string SourceSystem = "SYN-HIS";

    /// <summary>A synthetic named clinician owner.</summary>
    public static CaseOwner Clinician(string id = "SYN-STAFF-0001") =>
        new() { Reference = id, Role = "clinician", FacilityId = "SYN-FAC-A" };

    /// <summary>The synthetic facility lead queue.</summary>
    public static CaseOwner FacilityLeadQueue() =>
        new() { Reference = "SYN-QUEUE-FAC-A", Role = "facility-lead", FacilityId = "SYN-FAC-A", IsQueue = true };

    /// <summary>A newly opened synthetic case.</summary>
    public static Case NewCase(DateTimeOffset receivedAt, string sourceResultId = "SYN-RESULT-0001", CaseOwner? owner = null, string? patientDisplayName = null, string? patientSecondaryIdentifier = null) =>
        new(Guid.NewGuid(), SourceSystem, sourceResultId, "SYN-PT-0001", "breast-screening", "SYN-FAC-A", owner ?? Clinician(), receivedAt, patientDisplayName, patientSecondaryIdentifier);

    /// <summary>A synthetic applied source event for <paramref name="case"/>.</summary>
    public static SourceEvent AppliedEvent(Case @case, string sourceEventId, DateTimeOffset at, CaseEventType type = CaseEventType.ScreeningResultReceived) =>
        new()
        {
            Id = Guid.NewGuid(),
            SourceSystem = SourceSystem,
            SourceEventId = sourceEventId,
            Type = type,
            CaseId = @case.Id,
            OccurredAt = at,
            ReceivedAt = at,
            PayloadJson = "{}",
            Disposition = SourceEventDisposition.Applied,
        };
}

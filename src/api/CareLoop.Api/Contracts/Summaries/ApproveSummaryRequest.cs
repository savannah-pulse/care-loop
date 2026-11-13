namespace CareLoop.Contracts.Summaries;

/// <summary>
/// Body of the request approving a drafted handoff summary.
/// </summary>
public sealed record ApproveSummaryRequest
{
    /// <summary>The text being approved. The approver may have edited the draft; what is approved is exactly this text.</summary>
    public required string ApprovedText { get; init; }
}

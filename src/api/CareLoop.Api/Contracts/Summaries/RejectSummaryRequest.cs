namespace CareLoop.Contracts.Summaries;

/// <summary>
/// Body of the request rejecting a drafted handoff summary.
/// </summary>
public sealed record RejectSummaryRequest
{
    /// <summary>Why the reviewer turned the draft down. Stored with the rejection and read when the drafts are evaluated.</summary>
    public required string Reason { get; init; }
}

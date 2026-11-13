namespace CareLoop.Contracts.Summaries;

/// <summary>
/// A handoff summary for a case, returned when one is drafted or approved.
/// </summary>
/// <remarks>
/// AI output is a draft. It becomes usable only when a person approves it, and it can never set
/// urgency, alter source data, close a case or send a message.
/// </remarks>
public sealed record HandoffSummaryResponse
{
    /// <summary>Identifier of the summary.</summary>
    public required Guid SummaryId { get; init; }

    /// <summary>The case the summary describes.</summary>
    public required Guid CaseId { get; init; }

    /// <summary>Whether the summary is a draft or approved.</summary>
    public required HandoffSummaryStatus Status { get; init; }

    /// <summary>The summary text.</summary>
    public required string Text { get; init; }

    /// <summary>Name of the AI provider that produced the draft.</summary>
    public required string DraftedBy { get; init; }

    /// <summary>When the draft was produced.</summary>
    public required DateTimeOffset DraftedAt { get; init; }

    /// <summary>Reference of the person who approved it; <see langword="null"/> while it is a draft.</summary>
    public string? ApprovedBy { get; init; }

    /// <summary>When it was approved; <see langword="null"/> while it is a draft.</summary>
    public DateTimeOffset? ApprovedAt { get; init; }
}

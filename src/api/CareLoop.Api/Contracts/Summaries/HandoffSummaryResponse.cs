namespace CareLoop.Contracts.Summaries;

/// <summary>
/// A handoff summary for a case, returned when one is drafted, approved or rejected.
/// </summary>
/// <remarks>
/// AI output is a draft. It becomes usable only when a person approves it, and it can never set
/// urgency, alter source data, close a case or send a message. The sections carry the facts each
/// statement rests on so that reviewing means checking, not trusting.
/// </remarks>
public sealed record HandoffSummaryResponse
{
    /// <summary>Identifier of the summary.</summary>
    public required Guid SummaryId { get; init; }

    /// <summary>The case the summary describes.</summary>
    public required Guid CaseId { get; init; }

    /// <summary>Where the summary stands with its reviewer.</summary>
    public required HandoffSummaryStatus Status { get; init; }

    /// <summary>The sections of the summary, in reading order.</summary>
    /// <remarks>Once approved, these are the sections as approved, which may differ from what was drafted.</remarks>
    public required IReadOnlyList<SummarySectionResponse> Sections { get; init; }

    /// <summary>The sections run together as plain text; what a reviewer signs and what the HIS receives as a <c>DocumentReference</c>.</summary>
    public required string Text { get; init; }

    /// <summary>What the draft reported as missing, stale or contradictory, for a person to resolve.</summary>
    public required IReadOnlyList<string> MissingOrConflictingFacts { get; init; }

    /// <summary>Whether a person must review this before it can be used. Always <see langword="true"/> while the status is <c>Draft</c>.</summary>
    public required bool HumanReviewRequired { get; init; }

    /// <summary>Name of the AI provider that produced the draft.</summary>
    public required string DraftedBy { get; init; }

    /// <summary>The model deployment that produced the draft.</summary>
    public required string ModelDeployment { get; init; }

    /// <summary>Version of the prompt and template used, so a reviewer can tell two drafts apart.</summary>
    public required string PromptVersion { get; init; }

    /// <summary>Why the draft was produced: because a person asked, or because a handoff was proposed.</summary>
    public required string Trigger { get; init; }

    /// <summary>BCP 47 language tag the summary is written in.</summary>
    public required string Language { get; init; }

    /// <summary>When the draft was produced.</summary>
    public required DateTimeOffset DraftedAt { get; init; }

    /// <summary>Reference of the person who approved it; <see langword="null"/> unless the status is <c>Approved</c>.</summary>
    public string? ApprovedBy { get; init; }

    /// <summary>When it was approved; <see langword="null"/> unless the status is <c>Approved</c>.</summary>
    public DateTimeOffset? ApprovedAt { get; init; }

    /// <summary>Reference of the person who rejected it; <see langword="null"/> unless the status is <c>Rejected</c>.</summary>
    public string? RejectedBy { get; init; }

    /// <summary>When it was rejected; <see langword="null"/> unless the status is <c>Rejected</c>.</summary>
    public DateTimeOffset? RejectedAt { get; init; }

    /// <summary>Why the reviewer rejected it; <see langword="null"/> unless the status is <c>Rejected</c>.</summary>
    public string? RejectionReason { get; init; }
}

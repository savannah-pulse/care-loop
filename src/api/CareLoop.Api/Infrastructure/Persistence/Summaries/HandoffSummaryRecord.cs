using CareLoop.Contracts.Summaries;

namespace CareLoop.Api.Infrastructure.Persistence.Summaries;

/// <summary>
/// A stored handoff summary: an AI draft and, once a person approves it, the approved text.
/// </summary>
/// <remarks>
/// A summary is not case state. Drafting stores a record here; approval additionally writes a
/// source event through the single write path so the approval is audited.
/// </remarks>
public sealed class HandoffSummaryRecord
{
    /// <summary>Identifier of the summary.</summary>
    public Guid Id { get; set; }

    /// <summary>The case the summary describes.</summary>
    public Guid CaseId { get; set; }

    /// <summary>Whether the summary is a draft or approved.</summary>
    public HandoffSummaryStatus Status { get; set; }

    /// <summary>The draft text, or the approved text once approved.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Name of the AI provider that produced the draft.</summary>
    public string DraftedBy { get; set; } = string.Empty;

    /// <summary>When the draft was produced.</summary>
    public DateTimeOffset DraftedAt { get; set; }

    /// <summary>Reference of the person who approved it.</summary>
    public string? ApprovedBy { get; set; }

    /// <summary>When it was approved.</summary>
    public DateTimeOffset? ApprovedAt { get; set; }
}

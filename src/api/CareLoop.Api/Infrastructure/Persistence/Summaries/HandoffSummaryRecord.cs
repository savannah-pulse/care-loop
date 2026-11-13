using CareLoop.Contracts.Summaries;

namespace CareLoop.Api.Infrastructure.Persistence.Summaries;

/// <summary>
/// A stored handoff summary: an AI draft and, once a person reviews it, the approved text or the rejection.
/// </summary>
/// <remarks>
/// A summary is not case state. Drafting stores a record here; approval additionally writes a
/// source event through the single write path so the approval is audited, and rejection stays here
/// because it changes nothing about the case.
/// <para>
/// The provenance columns are the point of the record: which deployment and prompt produced the
/// text, from which facts, at whose request. Without them an approved summary is a paragraph with
/// no history, and a bad draft cannot be traced back to what produced it.
/// </para>
/// <para>
/// What is kept is the draft and the approved artifact. Raw prompts and model reasoning are not
/// stored, and the clinical facts a draft was built from stay in the source system; only the fact
/// identifiers and source references are kept here.
/// </para>
/// </remarks>
public sealed class HandoffSummaryRecord
{
    /// <summary>Identifier of the summary.</summary>
    public Guid Id { get; set; }

    /// <summary>The case the summary describes.</summary>
    public Guid CaseId { get; set; }

    /// <summary>Correlates the stored record with the provider call and the audit entry.</summary>
    public Guid RequestId { get; set; }

    /// <summary>Where the summary stands with its reviewer.</summary>
    public HandoffSummaryStatus Status { get; set; }

    /// <summary>The sections and their cited fact identifiers, as JSON.</summary>
    /// <remarks>Stored as JSON rather than as rows because a section has no meaning apart from its summary and is never queried on its own.</remarks>
    public string SectionsJson { get; set; } = string.Empty;

    /// <summary>The sections run together as plain text: what a reviewer signs and what becomes the <c>DocumentReference</c>.</summary>
    /// <remarks>Held rather than derived so that the text a person approved can never be changed by a later change to how sections are flattened.</remarks>
    public string Text { get; set; } = string.Empty;

    /// <summary>What the draft reported as missing, stale or contradictory, as JSON.</summary>
    public string MissingOrConflictingFactsJson { get; set; } = "[]";

    /// <summary>Identifiers and source references of the facts the draft was given, as JSON.</summary>
    /// <remarks>Keeps the trail to the source records without copying clinical content into the coordination store.</remarks>
    public string SourceFactReferencesJson { get; set; } = "[]";

    /// <summary>Name of the AI provider that produced the draft.</summary>
    public string DraftedBy { get; set; } = string.Empty;

    /// <summary>The model deployment that produced the draft.</summary>
    public string ModelDeployment { get; set; } = string.Empty;

    /// <summary>Version of the prompt and template used.</summary>
    public string PromptVersion { get; set; } = string.Empty;

    /// <summary>Version of the pathway definition the facts were read against.</summary>
    public string PolicyVersion { get; set; } = string.Empty;

    /// <summary>Why the draft was produced: <c>Requested</c> or <c>HandoffProposed</c>.</summary>
    public string Trigger { get; set; } = string.Empty;

    /// <summary>BCP 47 language tag the summary is written in.</summary>
    public string Language { get; set; } = "en";

    /// <summary>When the draft was produced.</summary>
    public DateTimeOffset DraftedAt { get; set; }

    /// <summary>Reference of the person who approved it.</summary>
    public string? ApprovedBy { get; set; }

    /// <summary>When it was approved.</summary>
    public DateTimeOffset? ApprovedAt { get; set; }

    /// <summary>Reference of the person who rejected it.</summary>
    public string? RejectedBy { get; set; }

    /// <summary>When it was rejected.</summary>
    public DateTimeOffset? RejectedAt { get; set; }

    /// <summary>Why the reviewer rejected it.</summary>
    public string? RejectionReason { get; set; }
}

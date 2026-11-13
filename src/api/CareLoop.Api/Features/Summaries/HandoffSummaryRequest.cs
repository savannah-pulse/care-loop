namespace CareLoop.Api.Features.Summaries;

/// <summary>
/// The facts given to an AI provider to draft a handoff summary.
/// </summary>
/// <remarks>
/// The provider is given this and nothing else: it cannot query the event store, call a source
/// system, choose a tool or receive the case object. Everything it may write about is a
/// <see cref="SummaryFact"/> here, each with an identifier the draft must cite.
/// <para>
/// Clinical facts a clinician finalized may be included (decision 9). Patient names, contact
/// details, images and unfinalized or conflicting interpretations never may: an image is not
/// summarisable text, and a conflict is for a person to resolve, not a model.
/// </para>
/// </remarks>
public sealed record HandoffSummaryRequest
{
    /// <summary>Correlates this request with the stored draft, the provider call and the audit record.</summary>
    public required Guid RequestId { get; init; }

    /// <summary>The case being handed over.</summary>
    public required Guid CaseId { get; init; }

    /// <summary>Identifier of the pathway the case follows.</summary>
    public required string PathwayId { get; init; }

    /// <summary>What the draft is for. Only <see cref="SummaryPurpose.ClinicianHandoff"/> is built today.</summary>
    public required SummaryPurpose Purpose { get; init; }

    /// <summary>Why the draft was asked for: a person clicked, or a handoff was proposed.</summary>
    public required DraftTrigger Trigger { get; init; }

    /// <summary>The facts the draft may use, oldest first. Every one is finalized.</summary>
    public required IReadOnlyList<SummaryFact> Facts { get; init; }

    /// <summary>The next action the backend or a clinician recorded, when there is one.</summary>
    /// <remarks><see langword="null"/> means no next action is recorded, and the draft must say so rather than propose one. A model may never invent the next step.</remarks>
    public SummaryFact? RecordedNextStep { get; init; }

    /// <summary>Facts the collector found unfinalized, conflicting or missing, named so the draft can flag them.</summary>
    /// <remarks>Carries the fact type and why it is unusable, never the unfinalized value itself.</remarks>
    public required IReadOnlyList<string> KnownGaps { get; init; }

    /// <summary>BCP 47 language tag the summary must be written in.</summary>
    /// <remarks><c>en</c> today. Arabic is deferred (<c>AGENTS.md</c>, "Not in scope yet"), and an unsupported tag fails closed rather than falling back to English.</remarks>
    public required string Language { get; init; }

    /// <summary>Version of the pathway definition the facts were read against.</summary>
    public required string PolicyVersion { get; init; }

    /// <summary>Version of the prompt and template the provider must use, recorded with the draft.</summary>
    public required string PromptVersion { get; init; }
}

using CareLoop.Domain.Cases;

namespace CareLoop.Api.Features.Summaries;

/// <summary>
/// Builds the fact set a handoff summary may be drafted from.
/// </summary>
/// <remarks>
/// This is the data boundary. Everything a provider may write about passes through here, so the
/// rules live in one place and one test can prove them: coordination facts from the case and its
/// timeline, and the clinician-finalized clinical facts the source system supplied (decision 9).
/// Patient names, contact details, images, and unfinalized or conflicting interpretations never
/// become facts; the last two are named in <see cref="HandoffSummaryRequest.KnownGaps"/> so the
/// draft flags them instead of a person discovering the silence later.
/// <para>
/// Clinical facts are not CareLoop's to keep. They are read for the length of one draft and are
/// not written to the coordination store: the clinical record stays in the source system.
/// </para>
/// </remarks>
public sealed class HandoffFactCollector
{
    /// <summary>
    /// Collects the facts for one case.
    /// </summary>
    /// <param name="case">The case being handed over.</param>
    /// <param name="purpose">What the draft is for; decides which facts are eligible.</param>
    /// <param name="trigger">Why the draft was asked for.</param>
    /// <param name="ct">Cancels the operation.</param>
    /// <returns>The request to give a provider.</returns>
    /// <exception cref="AiDraftUnavailableException">No finalized fact is available, so there is nothing to summarise.</exception>
    public Task<HandoffSummaryRequest> CollectAsync(Case @case, SummaryPurpose purpose, DraftTrigger trigger, CancellationToken ct)
    {
        // TODO(WS-9): build facts from the case (stage, owner, next action, due time, unresolved exceptions)
        // and its timeline, give each a stable FactId derived from its source event, fetch the
        // clinician-finalized clinical facts for the case's result reference, drop anything not
        // Finalized into KnownGaps, and stamp the pathway version and prompt version.
        throw new NotImplementedException("WS9: HandoffFactCollector.CollectAsync");
    }
}

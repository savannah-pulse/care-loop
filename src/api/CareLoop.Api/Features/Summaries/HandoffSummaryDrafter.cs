using CareLoop.Api.Infrastructure.Persistence.Summaries;
using CareLoop.Domain.Cases;

namespace CareLoop.Api.Features.Summaries;

/// <summary>
/// Produces and stores one handoff summary draft.
/// </summary>
/// <remarks>
/// Shared by the two triggers so that a draft made because a person asked and a draft made because
/// a handoff was proposed (decision 11) go through exactly the same collection, validation and
/// storage. One path means one set of guarantees.
/// <para>
/// Validation happens here as well as in the provider, because the provider may be a service
/// across the network: a draft whose sections cite a fact identifier that was not supplied, or
/// whose language is not the one requested, is refused and nothing is stored.
/// </para>
/// </remarks>
public sealed class HandoffSummaryDrafter
{
    private readonly HandoffFactCollector _facts;
    private readonly IAiProvider _provider;

    /// <summary>
    /// Creates the drafter.
    /// </summary>
    /// <param name="facts">Builds the fact set the draft may use.</param>
    /// <param name="provider">The configured AI provider.</param>
    public HandoffSummaryDrafter(HandoffFactCollector facts, IAiProvider provider)
    {
        _facts = facts;
        _provider = provider;
    }

    /// <summary>
    /// Drafts a summary for a case and stores it in status <c>Draft</c>.
    /// </summary>
    /// <param name="case">The case to summarise.</param>
    /// <param name="trigger">Why the draft is being produced.</param>
    /// <param name="ct">Cancels the operation.</param>
    /// <returns>The stored draft.</returns>
    /// <exception cref="InvalidOperationException">AI is switched off, so no draft can be produced.</exception>
    /// <exception cref="AiDraftUnavailableException">The provider failed or returned something that did not validate; nothing is stored.</exception>
    public Task<HandoffSummaryRecord> DraftAsync(Case @case, DraftTrigger trigger, CancellationToken ct)
    {
        // TODO(WS-9): collect facts, call IAiProvider, check every cited fact id exists and the language matches,
        // flatten the sections to the text that will become the DocumentReference, and store a
        // HandoffSummaryRecord in status Draft with the request id, deployment and prompt version.
        throw new NotImplementedException("WS9: HandoffSummaryDrafter.DraftAsync");
    }
}

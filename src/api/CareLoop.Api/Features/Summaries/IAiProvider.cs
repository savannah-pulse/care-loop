namespace CareLoop.Api.Features.Summaries;

/// <summary>
/// Drafts a handoff summary with AI.
/// </summary>
/// <remarks>
/// This seam keeps AI optional and replaceable: the no-op implementation is the default, so every
/// feature runs with AI switched off, and <c>Ai:Provider</c> chooses between calling the Python AI
/// workflow service (the agreed route, decision 8) and calling a Foundry deployment straight from
/// the API. Whatever an implementation returns is a draft for a person to approve. A provider can
/// never set urgency, alter source data, close a case or send a message, because it is given no
/// way to do so: it receives a <see cref="HandoffSummaryRequest"/> and returns text.
/// <para>
/// An implementation fails closed. A timeout, an unreachable endpoint, a content-filter refusal,
/// malformed output or a citation of an unknown fact identifier must raise
/// <see cref="AiDraftUnavailableException"/>, never a plausible-looking draft.
/// </para>
/// </remarks>
public interface IAiProvider
{
    /// <summary>Name of the provider, recorded with every draft.</summary>
    string Name { get; }

    /// <summary><see langword="false"/> when AI is switched off or unconfigured, and no draft can be produced.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Drafts a handoff summary from the supplied facts.
    /// </summary>
    /// <param name="request">The facts the draft may use; nothing else is available to the provider.</param>
    /// <param name="ct">Cancels the operation.</param>
    /// <returns>The draft, or <see langword="null"/> when <see cref="IsEnabled"/> is <see langword="false"/>.</returns>
    /// <exception cref="AiDraftUnavailableException">The provider could not produce a valid draft. No fallback text is ever returned.</exception>
    Task<AiDraft?> DraftHandoffSummaryAsync(HandoffSummaryRequest request, CancellationToken ct);
}

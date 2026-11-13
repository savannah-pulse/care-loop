namespace CareLoop.Api.Features.Summaries;

/// <summary>
/// Drafts text with AI.
/// </summary>
/// <remarks>
/// This seam keeps AI optional and replaceable: the no-op implementation is the default so every
/// feature runs with AI switched off. Whatever an implementation returns is a draft for a person
/// to approve. A provider can never set urgency, alter source data, close a case or send a message,
/// because it is given no way to do so.
/// </remarks>
public interface IAiProvider
{
    /// <summary>Name of the provider, recorded with every draft.</summary>
    string Name { get; }

    /// <summary><see langword="false"/> when AI is switched off and no draft can be produced.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Drafts a handoff summary from coordination facts.
    /// </summary>
    /// <param name="request">References, statuses and timestamps describing the case; no patient detail.</param>
    /// <param name="ct">Cancels the operation.</param>
    /// <returns>The draft, or <see langword="null"/> when <see cref="IsEnabled"/> is <see langword="false"/>.</returns>
    Task<AiDraft?> DraftHandoffSummaryAsync(HandoffSummaryRequest request, CancellationToken ct);
}

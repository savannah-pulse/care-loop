namespace CareLoop.Api.Features.Summaries;

/// <summary>
/// One section of a drafted summary and the facts it was built from.
/// </summary>
public sealed record AiDraftSection
{
    /// <summary>Heading of the section.</summary>
    public required string Heading { get; init; }

    /// <summary>The drafted text of the section.</summary>
    public required string Text { get; init; }

    /// <summary>Identifiers of the facts this section rests on.</summary>
    /// <remarks>
    /// Every identifier must be one of the <see cref="SummaryFact.FactId"/> values supplied in the
    /// request. An empty list is only valid for a section that states something is missing, because
    /// an absence has no fact to cite.
    /// </remarks>
    public required IReadOnlyList<string> SourceFactIds { get; init; }
}

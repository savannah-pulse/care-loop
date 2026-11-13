namespace CareLoop.Contracts.Summaries;

/// <summary>
/// One section of a handoff summary and the facts it rests on.
/// </summary>
public sealed record SummarySectionResponse
{
    /// <summary>Heading of the section.</summary>
    public required string Heading { get; init; }

    /// <summary>The text of the section.</summary>
    public required string Text { get; init; }

    /// <summary>Identifiers of the facts the section was drafted from, so a reviewer can check each statement against its source.</summary>
    public required IReadOnlyList<string> SourceFactIds { get; init; }
}

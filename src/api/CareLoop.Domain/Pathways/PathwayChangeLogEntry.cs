namespace CareLoop.Domain.Pathways;

/// <summary>
/// One entry in a pathway definition's change history: who approved which version, when, and what changed.
/// </summary>
/// <remarks>
/// A deadline or closure rule in force at the time of a decision must be traceable to the person
/// who approved it. The change log makes the history of the definition visible on the programme
/// screen without reading version control. Until a clinical adviser signs off, every entry is a
/// placeholder and must say so in <see cref="Approver"/>.
/// </remarks>
public sealed record PathwayChangeLogEntry
{
    /// <summary>The definition version the entry describes; matches <see cref="PathwayDefinition.Version"/> for the current entry.</summary>
    public required string Version { get; init; }

    /// <summary>The date the version was approved or, for a placeholder, drafted.</summary>
    public required DateOnly Date { get; init; }

    /// <summary>Who approved the version. A placeholder entry names no clinical adviser and says it is unapproved.</summary>
    public required string Approver { get; init; }

    /// <summary>What changed in this version, in one or two sentences.</summary>
    public required string Summary { get; init; }
}

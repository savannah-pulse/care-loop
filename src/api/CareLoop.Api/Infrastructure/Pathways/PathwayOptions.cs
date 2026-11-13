namespace CareLoop.Api.Infrastructure.Pathways;

/// <summary>
/// Where pathway definitions are loaded from, bound to the <c>Pathways</c> configuration section.
/// </summary>
/// <remarks>
/// Deadlines are configuration, so which set of definitions a deployment runs on is configuration
/// too. The default folder holds the real definitions. Development and demos point at the
/// <c>demo</c> subfolder, whose files are identical except that every duration is shortened so a
/// journey can be shown live. CareLoop itself always runs on real time: it never speeds up its
/// clock, it only reads shorter deadlines.
/// </remarks>
public sealed class PathwayOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "Pathways";

    /// <summary>
    /// Folder holding the pathway definition files, relative to the application's content root.
    /// Only the <c>*.json</c> files directly in this folder are loaded, never its subfolders.
    /// </summary>
    public string Directory { get; set; } = "Infrastructure/Pathways";
}

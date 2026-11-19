namespace CareLoop.Api.Infrastructure.Ai;

/// <summary>
/// The prompt files: one plain-text file per AI use, holding the instructions and a <c>{{data}}</c> marker where the
/// API puts the data to summarise.
/// </summary>
/// <remarks>
/// To change what the model is asked, edit the file and restart the API. Files are read once, at start-up, and a
/// missing file or one without exactly one <c>{{data}}</c> stops the host.
/// </remarks>
public sealed class AiPrompts
{
    /// <summary>Where the data goes in a prompt file.</summary>
    public const string DataMarker = "{{data}}";

    /// <summary>The role briefing's prompt file, <c>role-briefing.md</c>.</summary>
    public const string RoleBriefing = "role-briefing";

    /// <summary>The impact summary's prompt file, <c>impact-summary.md</c>.</summary>
    public const string ImpactSummary = "impact-summary";

    /// <summary>The patient message draft's prompt file, <c>patient-message.md</c>.</summary>
    public const string PatientMessage = "patient-message";

    private readonly Dictionary<string, string> _files = [];

    /// <summary>Reads and checks the prompt files.</summary>
    /// <param name="directory">The folder holding them.</param>
    /// <exception cref="InvalidOperationException">A file is missing or does not contain <c>{{data}}</c> exactly once.</exception>
    public AiPrompts(string directory)
    {
        foreach (var name in new[] { RoleBriefing, ImpactSummary, PatientMessage })
        {
            var path = Path.Combine(directory, $"{name}.md");
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"Prompt file '{path}' does not exist (Ai:PromptsDirectory).");
            }

            var text = File.ReadAllText(path);
            if (text.Split(DataMarker).Length != 2)
            {
                throw new InvalidOperationException($"Prompt file '{path}' must contain {DataMarker} exactly once: it marks where the data goes.");
            }

            _files[name] = text;
        }
    }

    /// <summary>The instructions of a prompt file: everything before the data marker.</summary>
    /// <param name="name">The prompt file's name, without <c>.md</c>.</param>
    /// <returns>The instructions, without the marker or the "Data:" label above it.</returns>
    public string Task(string name)
    {
        var text = _files[name][.._files[name].IndexOf(DataMarker, StringComparison.Ordinal)].TrimEnd();
        return text.EndsWith("Data:", StringComparison.OrdinalIgnoreCase) ? text[..^5].TrimEnd() : text;
    }

    /// <summary>Puts the data into a prompt file.</summary>
    /// <param name="name">The prompt file's name, without <c>.md</c>.</param>
    /// <param name="data">The data, as JSON.</param>
    /// <returns>The prompt exactly as it is sent.</returns>
    public string Render(string name, string data) => _files[name].Replace(DataMarker, data, StringComparison.Ordinal);
}

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace CareLoop.Api.Features.Agents;

/// <summary>
/// Writes a prompt the way the Foundry agents read one: a first line naming the prompt version, then the facts as one
/// JSON object inside <c>&lt;input_json&gt;</c>.
/// </summary>
/// <remarks>
/// Each agent's instructions live in the Foundry project and name the fields they expect inside <c>&lt;input_json&gt;</c>
/// (see <c>docs/ai-design/integration.md</c>, "What each agent expects and returns"). The prompt carries no instruction
/// of its own: the agents treat instructions inside the data as a reason to refuse. Property names are written in
/// snake case, as the agents name them; dictionary keys, such as the briefing's band names, are written as given.
/// </remarks>
internal static class AgentInput
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        // Every value is built by the API from settings, codes and its own wording, never from free text, so nothing
        // needs HTML escaping; plain JSON keeps times such as 09:00+04:00 and words such as reader's readable.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Writes the prompt.
    /// </summary>
    /// <param name="title">What the prompt is, for example <c>CareLoop role briefing</c>.</param>
    /// <param name="promptVersion">The prompt's version, for example <c>role-briefing-v3</c>.</param>
    /// <param name="input">The facts, built only from fields the calling endpoint allows.</param>
    /// <returns>The prompt: the title and prompt version on the first line, then the facts inside <c>&lt;input_json&gt;</c>.</returns>
    public static string Prompt(string title, string promptVersion, object input) =>
        $"{title}, prompt version {promptVersion}.\n<input_json>\n{JsonSerializer.Serialize(input, input.GetType(), Json)}\n</input_json>\n";

    /// <summary>
    /// Reads back the facts a prompt carries, for tests and checks.
    /// </summary>
    /// <param name="prompt">A prompt written by <see cref="Prompt"/>.</param>
    /// <returns>The JSON inside <c>&lt;input_json&gt;</c>.</returns>
    public static JsonDocument Facts(string prompt)
    {
        var start = prompt.IndexOf("<input_json>", StringComparison.Ordinal) + "<input_json>".Length;
        var end = prompt.IndexOf("</input_json>", StringComparison.Ordinal);
        return JsonDocument.Parse(prompt[start..end]);
    }

    /// <summary>A moment in local time as the agents read it, for example <c>2026-11-19T09:00+04:00</c>.</summary>
    /// <param name="local">The moment, already in local time.</param>
    /// <returns>ISO 8601 to the minute, with the offset.</returns>
    public static string Moment(DateTimeOffset local) => local.ToString("yyyy-MM-dd'T'HH:mmzzz", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A local day as the agents read it, for example <c>2026-11-19</c>.</summary>
    /// <param name="day">The day.</param>
    /// <returns>ISO 8601 date.</returns>
    public static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}

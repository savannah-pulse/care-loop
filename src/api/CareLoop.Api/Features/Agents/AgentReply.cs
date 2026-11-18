using System.Text.Json;
using System.Text.RegularExpressions;

namespace CareLoop.Api.Features.Agents;

/// <summary>
/// Thrown when an agent answered but gave no draft CareLoop can show: it declined (<c>cannot_comply</c>), or its answer
/// broke the agent's output contract.
/// </summary>
/// <remarks>
/// The endpoint answers it like any other failure, with the calm 503 "AI could not write this". The reason code goes to
/// the log with the call's correlation identifier, which the read audit already holds; it is never shown on screen.
/// </remarks>
public sealed class AgentDeclinedException : Exception
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="reasonCode">The agent's own reason code, such as <c>MISSING_FIELD</c>, or CareLoop's, such as <c>LOCKED_SPAN_CHANGED</c>.</param>
    public AgentDeclinedException(string reasonCode)
        : base($"The agent gave no usable draft ({reasonCode}).") => ReasonCode = reasonCode;

    /// <summary>A short upper-case code, safe to log: never words the agent wrote about a case.</summary>
    public string ReasonCode { get; }
}

/// <summary>
/// Reads the JSON object an agent answers with, and recognises a refusal in any of the shapes the agents use.
/// </summary>
/// <remarks>
/// The agents have no response schema set in the Foundry project, so the same agent version words a refusal more than
/// one way: <c>{"status":"cannot_comply","reason_code":"…"}</c>, <c>{"cannot_comply":{"reason_code":"…"}}</c>, or plain
/// text such as <c>cannot_comply: TEMPLATE_INVALID</c>. An answer may also come inside a Markdown code fence. Each of
/// those is read here, so every endpoint treats a refusal the same way.
/// </remarks>
internal static partial class AgentReply
{
    /// <summary>
    /// Reads the agent's answer as one JSON object.
    /// </summary>
    /// <param name="text">The agent's text, as the client returned it.</param>
    /// <returns>The answer's root object.</returns>
    /// <exception cref="AgentDeclinedException">The agent declined, with its reason code; or the answer holds no JSON object (<c>NOT_JSON</c>).</exception>
    public static JsonElement Read(string text)
    {
        var body = text.Trim();
        if (Fence().Match(body) is { Success: true } fence)
        {
            body = fence.Groups["body"].Value.Trim();
        }

        var start = body.IndexOf('{', StringComparison.Ordinal);
        var end = body.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            throw new AgentDeclinedException(PlainRefusal().Match(body) is { Success: true } refusal ? Code(refusal.Groups["code"].Value) : "NOT_JSON");
        }

        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(body[start..(end + 1)]);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new AgentDeclinedException("NOT_JSON");
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new AgentDeclinedException("NOT_JSON");
        }

        if (Refusal(root) is { } reason)
        {
            throw new AgentDeclinedException(reason);
        }

        return root;
    }

    /// <summary>A string property, trimmed; <see langword="null"/> when it is missing, not a string or blank.</summary>
    /// <param name="element">The object.</param>
    /// <param name="name">The property.</param>
    /// <returns>The trimmed string, or <see langword="null"/>.</returns>
    public static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;

    /// <summary>A required string property.</summary>
    /// <param name="element">The object.</param>
    /// <param name="name">The property.</param>
    /// <returns>The trimmed string.</returns>
    /// <exception cref="AgentDeclinedException">The property is missing or blank (<c>MISSING_OUTPUT_FIELD</c>).</exception>
    public static string RequiredText(JsonElement element, string name) =>
        Text(element, name) ?? throw new AgentDeclinedException("MISSING_OUTPUT_FIELD");

    /// <summary>The strings in an array property; empty when it is missing.</summary>
    /// <param name="element">The object.</param>
    /// <param name="name">The property.</param>
    /// <returns>Each string, trimmed, in order.</returns>
    /// <exception cref="AgentDeclinedException">The property is not an array of strings (<c>MALFORMED_OUTPUT</c>).</exception>
    public static IReadOnlyList<string> Texts(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        if (value.ValueKind != JsonValueKind.Array || value.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
        {
            throw new AgentDeclinedException("MALFORMED_OUTPUT");
        }

        return value.EnumerateArray().Select(item => item.GetString()!.Trim()).ToList();
    }

    /// <summary>The objects in a required array property.</summary>
    /// <param name="element">The object.</param>
    /// <param name="name">The property.</param>
    /// <returns>Each object, in order.</returns>
    /// <exception cref="AgentDeclinedException">The property is missing (<c>MISSING_OUTPUT_FIELD</c>) or holds something other than objects (<c>MALFORMED_OUTPUT</c>).</exception>
    public static IReadOnlyList<JsonElement> Objects(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            throw new AgentDeclinedException("MISSING_OUTPUT_FIELD");
        }

        return value.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.Object)
            ? throw new AgentDeclinedException("MALFORMED_OUTPUT")
            : value.EnumerateArray().ToList();
    }

    // A refusal is status "cannot_comply" (or "refused"), or a "cannot_comply" property that is not false or null.
    private static string? Refusal(JsonElement root)
    {
        if (root.TryGetProperty("cannot_comply", out var nested) && nested.ValueKind is not (JsonValueKind.False or JsonValueKind.Null))
        {
            return ReasonCode(nested) ?? ReasonCode(root)
                ?? (nested.ValueKind == JsonValueKind.String ? Code(nested.GetString()) : "OTHER");
        }

        return Text(root, "status") is { } status
            && (status.Equals("cannot_comply", StringComparison.OrdinalIgnoreCase) || status.Equals("refused", StringComparison.OrdinalIgnoreCase))
            ? ReasonCode(root) ?? "OTHER"
            : null;
    }

    private static string? ReasonCode(JsonElement element) => Text(element, "reason_code") is { } code ? Code(code) : null;

    // Only a short code reaches the log; anything longer, or anything that is not a code, is OTHER.
    private static string Code(string? code) =>
        code is not null && CodePattern().IsMatch(code.Trim()) ? code.Trim().ToUpperInvariant() : "OTHER";

    [GeneratedRegex(@"^```[A-Za-z]*\s*(?<body>[\s\S]*?)\s*```$")]
    private static partial Regex Fence();

    [GeneratedRegex(@"cannot[_ ]comply\W+(?<code>[A-Z][A-Z_]{2,48})")]
    private static partial Regex PlainRefusal();

    [GeneratedRegex("^[A-Za-z][A-Za-z_]{1,48}$")]
    private static partial Regex CodePattern();
}

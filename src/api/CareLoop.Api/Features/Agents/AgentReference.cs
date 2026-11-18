using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace CareLoop.Api.Features.Agents;

/// <summary>
/// One version of one Foundry agent: its name in the project and the version CareLoop pins it to.
/// </summary>
/// <remarks>
/// <para>
/// Written <c>name:version</c>, for example <c>role-briefing:2</c>, the way Foundry names an agent version. Calling an
/// agent by name alone follows whichever version the project serves, so an edit in the Foundry portal could change what
/// staff read while CareLoop's prompt version stayed the same. Every call therefore names the version, and the audit
/// records it.
/// </para>
/// <para>
/// Both parts are one URL path segment of letters, digits, <c>-</c>, <c>_</c> or <c>.</c>, so neither can reach another
/// address or carry anything but a name.
/// </para>
/// </remarks>
/// <param name="Name">The agent's name in the Foundry project, for example <c>role-briefing</c>.</param>
/// <param name="Version">The pinned version, for example <c>2</c>.</param>
public sealed partial record AgentReference(string Name, string Version)
{
    /// <summary>Reads a <c>name:version</c> setting.</summary>
    /// <param name="value">The setting, for example <c>role-briefing:2</c>.</param>
    /// <param name="reference">The agent version, when the setting is well formed.</param>
    /// <returns><see langword="true"/> when the setting names both an agent and a version.</returns>
    public static bool TryParse(string? value, [NotNullWhen(true)] out AgentReference? reference)
    {
        reference = null;
        var match = value is null ? null : Pattern().Match(value.Trim());
        if (match is not { Success: true })
        {
            return false;
        }

        reference = new AgentReference(match.Groups["name"].Value, match.Groups["version"].Value);
        return true;
    }

    /// <summary>Reads a <c>name:version</c> setting that start-up has already checked.</summary>
    /// <param name="value">The setting.</param>
    /// <returns>The agent version.</returns>
    /// <exception cref="InvalidOperationException">The setting does not name both an agent and a version.</exception>
    public static AgentReference Parse(string? value) =>
        TryParse(value, out var reference)
            ? reference
            : throw new InvalidOperationException($"'{value}' is not an agent version: write it as name:version, for example role-briefing:2.");

    /// <summary>Writes the agent version the way the settings and the audit hold it.</summary>
    /// <returns>For example <c>role-briefing:2</c>.</returns>
    public override string ToString() => $"{Name}:{Version}";

    [GeneratedRegex("^(?<name>[A-Za-z0-9][A-Za-z0-9._-]{0,127}):(?<version>[A-Za-z0-9][A-Za-z0-9._-]{0,63})$")]
    private static partial Regex Pattern();
}

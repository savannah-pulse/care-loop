using System.Text.RegularExpressions;

namespace CareLoop.Api.Infrastructure.Ai;

/// <summary>
/// Settings for the three Microsoft Foundry agents the dashboard asks for words, bound to the <c>Ai:Agents</c>
/// configuration section: the role briefing, the impact summary and the patient message draft.
/// </summary>
/// <remarks>
/// <para>
/// Off by default, so the API runs with no Foundry project and no credential, and the three endpoints answer 501.
/// When <see cref="Enabled"/> is <see langword="true"/> the settings are checked at start-up
/// (<see cref="IsValid"/>), so a missing or malformed project endpoint stops the host instead of failing the first
/// request.
/// </para>
/// <para>
/// There is no API key. The agents are called with an Entra token for the API's own identity, a managed identity
/// when hosted in Azure, which needs the <c>Azure AI User</c> role on the Foundry project. The project endpoint names
/// a real resource, so it belongs in the environment's configuration, never in a committed file.
/// </para>
/// <para>
/// These agents are separate from the handoff summary, which goes through <c>IAiProvider</c> and <c>Ai:Provider</c>.
/// Whatever they write is a draft for a person to read: they cannot set urgency, change a case or send a message.
/// </para>
/// </remarks>
public sealed partial class AgentOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "Ai:Agents";

    /// <summary>Longest wait the settings accept for one agent, in seconds.</summary>
    public const int MaximumTimeoutSeconds = 300;

    /// <summary>Whether the agents are switched on. While <see langword="false"/>, the three AI endpoints answer 501 and nothing is sent anywhere.</summary>
    public bool Enabled { get; set; }

    /// <summary>The Foundry project endpoint, in the form <c>https://{resource}.services.ai.azure.com/api/projects/{project}</c>.</summary>
    public string? ProjectEndpoint { get; set; }

    /// <summary>Name of the agent that writes a short briefing from the reader's worklist.</summary>
    public string RoleBriefing { get; set; } = "role-briefing";

    /// <summary>Name of the agent that writes what the aggregate numbers on the Impact screen say.</summary>
    public string ImpactSummary { get; set; } = "impact-summary";

    /// <summary>Name of the agent that drafts a message to a patient, as a preview that is never sent.</summary>
    public string PatientNotification { get; set; } = "patient-notification";

    /// <summary>How long to wait for one agent, token included, before reporting that AI could not write the text.</summary>
    /// <remarks>Bounded so a slow agent cannot hold a request open; from 1 to <see cref="MaximumTimeoutSeconds"/>.</remarks>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// A ready Entra access token for the scope <c>https://ai.azure.com/.default</c>, used instead of the API's identity.
    /// </summary>
    /// <remarks>
    /// Honoured only in the <c>Development</c> environment, so a developer can test the agents with a token from their own
    /// sign-in. Anywhere else it is ignored and a warning is logged. Set it from the environment
    /// (<c>Ai__Agents__AccessToken</c>) for one session; never commit it.
    /// </remarks>
    public string? AccessToken { get; set; }

    /// <summary>
    /// The clinician-reviewed urgency codes the role briefing counts and orders as urgent.
    /// </summary>
    /// <remarks>
    /// Urgency arrives as an opaque code from the hospital system and CareLoop never sets it. This list only says which
    /// codes mean urgent, so the briefing can put urgent cases first. It must match the dashboard's table of urgency
    /// codes, and like the pathway's codes it is a placeholder until a clinical adviser approves it.
    /// </remarks>
    public List<string> UrgentCodes { get; set; } = [];

    /// <summary>
    /// Checks the settings that matter once the agents are switched on.
    /// </summary>
    /// <param name="options">The settings to check.</param>
    /// <returns>
    /// <see langword="true"/> when the agents are off, or when the project endpoint is an absolute <c>https</c> address,
    /// each agent name is one URL path segment of letters, digits, <c>-</c>, <c>_</c> or <c>.</c>, and
    /// <see cref="TimeoutSeconds"/> is from 1 to <see cref="MaximumTimeoutSeconds"/>.
    /// </returns>
    /// <remarks>The token travels to the endpoint, so a plain <c>http</c> address is refused.</remarks>
    public static bool IsValid(AgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Enabled)
        {
            return true;
        }

        return Uri.TryCreate(options.ProjectEndpoint?.Trim(), UriKind.Absolute, out var endpoint)
            && endpoint.Scheme == Uri.UriSchemeHttps
            && IsAgentName(options.RoleBriefing)
            && IsAgentName(options.ImpactSummary)
            && IsAgentName(options.PatientNotification)
            && options.TimeoutSeconds is >= 1 and <= MaximumTimeoutSeconds;
    }

    private static bool IsAgentName(string? name) => name is not null && AgentName().IsMatch(name);

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")]
    private static partial Regex AgentName();
}

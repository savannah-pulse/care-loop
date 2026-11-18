using CareLoop.Api.Features.Agents;

namespace CareLoop.Api.Infrastructure.Ai;

/// <summary>
/// Settings for the three Microsoft Foundry agents the dashboard asks for words, bound to the <c>Ai:Agents</c>
/// configuration section: whether they are on, and which agent version writes the role briefing, the impact summary
/// and the patient message preview.
/// </summary>
/// <remarks>
/// <para>
/// Everything else comes from the shared <c>Ai</c> settings the handoff summary uses (<see cref="AiOptions"/>): the
/// route (<c>Ai:Provider</c>), the wait (<c>Ai:TimeoutSeconds</c>), the request-size limit (<c>Ai:MaxRequestBytes</c>),
/// the Foundry project (<c>Ai:FoundryDirect:ProjectEndpoint</c>) and the Python service (<c>Ai:AiWorkflow:BaseUrl</c>).
/// </para>
/// <para>
/// Off by default, so the API runs with no Foundry project and no credential, and the three endpoints answer 409. When
/// <see cref="Enabled"/> is <see langword="true"/> the settings are checked at start-up (<see cref="IsValid"/>), so a
/// missing or malformed setting stops the host instead of failing the first request.
/// </para>
/// <para>
/// There is no API key. On the <c>FoundryDirect</c> route the agents are called with an Entra token for the API's own
/// identity, a managed identity when hosted in Azure, which needs the <c>Foundry User</c> role (formerly
/// <c>Azure AI User</c>) on the Foundry project. Whatever the agents write is a draft for a person to read: they cannot
/// set urgency, change a case or send a message.
/// </para>
/// </remarks>
public sealed class AgentOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "Ai:Agents";

    /// <summary>Whether the agents are switched on. While <see langword="false"/>, the three AI endpoints answer 409 and nothing is sent anywhere.</summary>
    /// <remarks>Separate from <c>Ai:Provider</c>, so the handoff summary can be switched on without the dashboard's agents.</remarks>
    public bool Enabled { get; set; }

    /// <summary>The agent version that writes a short briefing from the reader's worklist, as <c>name:version</c>.</summary>
    /// <remarks>The default matches <c>src/ai_agents/rolebriefing/run_agent.py</c>; change both together.</remarks>
    public string RoleBriefing { get; set; } = "role-briefing:2";

    /// <summary>The agent version that writes what the aggregate numbers on the Impact screen say, as <c>name:version</c>.</summary>
    /// <remarks>The default matches <c>src/ai_agents/impactsummary/run_agent.py</c>; change both together.</remarks>
    public string ImpactSummary { get; set; } = "impact-summary:4";

    /// <summary>The agent version that drafts a message to a patient, as a preview that is never sent, as <c>name:version</c>.</summary>
    /// <remarks>The default matches <c>src/ai_agents/patientnotification/run_agent.py</c>; change both together.</remarks>
    public string PatientNotification { get; set; } = "patient-notification:2";

    /// <summary>
    /// A ready Entra access token for the scope <c>https://ai.azure.com/.default</c>, used on the <c>FoundryDirect</c>
    /// route instead of the API's identity.
    /// </summary>
    /// <remarks>
    /// The one exception to "no token in configuration": honoured only in the <c>Development</c> environment, so a
    /// developer can test the agents with a token from their own sign-in. Anywhere else it is ignored and a warning is
    /// logged. Set it from the environment (<c>Ai__Agents__AccessToken</c>) for one session; never commit it.
    /// </remarks>
    public string? AccessToken { get; set; }

    /// <summary>
    /// The clinician-reviewed urgency codes the role briefing counts and orders as urgent.
    /// </summary>
    /// <remarks>
    /// Not a connection setting: urgency arrives as an opaque code from the hospital system and CareLoop never sets it.
    /// This list only says which codes mean urgent, so the briefing can put urgent cases first. It must match the
    /// dashboard's table of urgency codes, and like the pathway's codes it is a placeholder until a clinical adviser
    /// approves it.
    /// </remarks>
    public List<string> UrgentCodes { get; set; } = [];

    /// <summary>The role-briefing agent version, read from <see cref="RoleBriefing"/>.</summary>
    /// <returns>The agent and its pinned version.</returns>
    public AgentReference RoleBriefingAgent() => AgentReference.Parse(RoleBriefing);

    /// <summary>The impact-summary agent version, read from <see cref="ImpactSummary"/>.</summary>
    /// <returns>The agent and its pinned version.</returns>
    public AgentReference ImpactSummaryAgent() => AgentReference.Parse(ImpactSummary);

    /// <summary>The patient-notification agent version, read from <see cref="PatientNotification"/>.</summary>
    /// <returns>The agent and its pinned version.</returns>
    public AgentReference PatientNotificationAgent() => AgentReference.Parse(PatientNotification);

    /// <summary>
    /// Checks the settings that matter once the agents are switched on.
    /// </summary>
    /// <param name="options">The agents' settings.</param>
    /// <param name="ai">The shared AI settings.</param>
    /// <returns>
    /// <see langword="true"/> when the agents are off; otherwise when each agent is a well-formed <c>name:version</c>,
    /// <c>Ai:TimeoutSeconds</c> is from 1 to <see cref="AiOptions.MaximumTimeoutSeconds"/>, <c>Ai:MaxRequestBytes</c> is
    /// positive, and the route has its address: an absolute <c>https</c> <c>Ai:FoundryDirect:ProjectEndpoint</c> for
    /// <c>FoundryDirect</c>, an absolute <c>http</c> or <c>https</c> <c>Ai:AiWorkflow:BaseUrl</c> for <c>AiWorkflow</c>.
    /// <c>NoOp</c> needs neither: the endpoints answer 409.
    /// </returns>
    /// <remarks>The token travels to the Foundry project, so a plain <c>http</c> project endpoint is refused.</remarks>
    public static bool IsValid(AgentOptions options, AiOptions ai)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(ai);
        if (!options.Enabled)
        {
            return true;
        }

        var agents = AgentReference.TryParse(options.RoleBriefing, out _)
            && AgentReference.TryParse(options.ImpactSummary, out _)
            && AgentReference.TryParse(options.PatientNotification, out _);
        var limits = ai.TimeoutSeconds is >= 1 and <= AiOptions.MaximumTimeoutSeconds && ai.MaxRequestBytes > 0;
        return agents && limits && ai.Route() switch
        {
            AiRoute.FoundryDirect => IsAbsolute(ai.FoundryDirect.ProjectEndpoint, Uri.UriSchemeHttps),
            AiRoute.AiWorkflow => IsAbsolute(ai.AiWorkflow.BaseUrl, Uri.UriSchemeHttps, Uri.UriSchemeHttp),
            _ => true,
        };
    }

    private static bool IsAbsolute(string? address, params string[] schemes) =>
        Uri.TryCreate(address?.Trim(), UriKind.Absolute, out var uri) && schemes.Contains(uri.Scheme);
}

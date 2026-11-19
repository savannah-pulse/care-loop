namespace CareLoop.Api.Infrastructure.Ai;

/// <summary>AI settings, bound to the <c>Ai</c> configuration section.</summary>
public sealed class AiOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "Ai";

    /// <summary>Which provider writes the texts: <c>NoOp</c> (default, AI off) or <c>Foundry</c>.</summary>
    public string Provider { get; set; } = "NoOp";

    /// <summary>How long to wait for an answer before reporting it unavailable.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Hours a role briefing or impact summary is reused while its data is unchanged; 0 switches reuse off.</summary>
    public double CacheHours { get; set; } = 6;

    /// <summary>Folder holding the prompt files; relative to the content root unless rooted.</summary>
    public string PromptsDirectory { get; set; } = "Prompts";

    /// <summary>Whether the data is synthetic. While <see langword="true"/>, the impact summary ends with a sentence saying so.</summary>
    /// <remarks><see langword="true"/> until real data is approved for this deployment.</remarks>
    public bool SyntheticData { get; set; } = true;

    /// <summary>Settings for the Microsoft Foundry provider.</summary>
    public FoundryOptions Foundry { get; set; } = new();
}

/// <summary>Settings for the agents in a Microsoft Foundry project, bound to <c>Ai:Foundry</c>.</summary>
/// <remarks>There is no API key: the agents are called with an Entra token (the managed identity in Azure, the signed-in developer locally).</remarks>
public sealed class FoundryOptions
{
    /// <summary>The project endpoint, <c>https://{resource}.services.ai.azure.com/api/projects/{project}</c>. Supplied by the environment, never committed.</summary>
    public string? ProjectEndpoint { get; set; }

    /// <summary>Which agent writes each text, by name: the key is the use (the prompt file's name), the value the agent's name in the project.</summary>
    /// <remarks>Names only, so the agent's latest version always answers. The agents themselves are owned and changed in the Foundry project, not here.</remarks>
    public Dictionary<string, string> Agents { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        [AiPrompts.RoleBriefing] = "role-briefing",
        [AiPrompts.ImpactSummary] = "impact-summary",
        [AiPrompts.PatientMessage] = "patient-notification",
    };
}

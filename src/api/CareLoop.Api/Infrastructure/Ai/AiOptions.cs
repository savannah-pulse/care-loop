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

    /// <summary>Settings for the Microsoft Foundry provider.</summary>
    public FoundryOptions Foundry { get; set; } = new();
}

/// <summary>Settings for calling a model in a Microsoft Foundry project, bound to <c>Ai:Foundry</c>.</summary>
/// <remarks>There is no API key: the model is called with an Entra token (the managed identity in Azure, the signed-in developer locally).</remarks>
public sealed class FoundryOptions
{
    /// <summary>The project endpoint, <c>https://{resource}.services.ai.azure.com/api/projects/{project}</c>. Supplied by the environment, never committed.</summary>
    public string? ProjectEndpoint { get; set; }

    /// <summary>The name of the model deployment that writes the texts, for example <c>gpt-6.1-sol</c>.</summary>
    public string? Model { get; set; }
}

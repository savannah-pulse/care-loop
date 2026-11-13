namespace CareLoop.Api.Infrastructure.Ai;

/// <summary>
/// AI settings, bound to the <c>Ai</c> configuration section.
/// </summary>
public sealed class AiOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "Ai";

    /// <summary>Which provider to use: <c>NoOp</c> (default, AI off) or <c>AzureOpenAi</c>.</summary>
    public string Provider { get; set; } = "NoOp";

    /// <summary>Settings for the Azure OpenAI provider.</summary>
    public AzureOpenAiOptions AzureOpenAi { get; set; } = new();
}

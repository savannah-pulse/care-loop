namespace CareLoop.Api.Infrastructure.Ai;

/// <summary>
/// Connection settings for Azure OpenAI. Supplied through environment variables, never committed.
/// </summary>
public sealed class AzureOpenAiOptions
{
    /// <summary>The resource endpoint URL.</summary>
    public string? Endpoint { get; set; }

    /// <summary>The model deployment name.</summary>
    public string? Deployment { get; set; }

    /// <summary>The API key. A secret: comes from the environment only.</summary>
    public string? ApiKey { get; set; }
}

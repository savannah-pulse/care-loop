namespace CareLoop.Api.Infrastructure.Ai;

/// <summary>
/// Connection settings for calling a Microsoft Foundry deployment directly.
/// </summary>
/// <remarks>
/// Supplied by the hosting environment, never committed. There is no API key: the deployment is
/// called with an Entra token, so no long-lived model credential exists to leak. Even though the
/// endpoint URL is not itself confidential, it names a real deployment and belongs in the
/// environment's configuration service rather than in the repository.
/// </remarks>
public sealed class FoundryOptions
{
    /// <summary>The Foundry project endpoint, in the form <c>https://{resource}.services.ai.azure.com/api/projects/{project}</c>.</summary>
    /// <remarks>Shared: the handoff summary's direct route calls its model deployment, and on the same route the dashboard's agents are called in this project.</remarks>
    public string? ProjectEndpoint { get; set; }

    /// <summary>Exact name of the model deployment.</summary>
    /// <remarks>The deployment name, not the catalogue model name: on the OpenAI-compatible path the deployment name is the model identifier, and the two are often different. The dashboard's agents do not use it: each agent version names its own model.</remarks>
    public string? DeploymentName { get; set; }
}

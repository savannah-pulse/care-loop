namespace CareLoop.Api.Infrastructure.Ai;

/// <summary>
/// Settings for calling the Python AI workflow service.
/// </summary>
public sealed class AiWorkflowOptions
{
    /// <summary>Base URL of the Python service, for example <c>http://ai-workflow:8080</c>.</summary>
    /// <remarks>An internal address. The service holds the Foundry credentials; the API holds none.</remarks>
    public string? BaseUrl { get; set; }
}

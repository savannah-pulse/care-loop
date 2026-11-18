namespace CareLoop.Api.Infrastructure.Ai;

/// <summary>
/// Which way CareLoop reaches a model, read from the shared <c>Ai:Provider</c> setting.
/// </summary>
/// <remarks>
/// One setting chooses the route for everything AI does, so the handoff summary and the dashboard's agents cannot
/// drift apart: AI is off, goes through the Python AI workflow service, or goes from the API to Foundry.
/// </remarks>
public enum AiRoute
{
    /// <summary>AI is switched off. Nothing is sent anywhere; the AI endpoints answer 409.</summary>
    NoOp = 0,

    /// <summary>The API calls the Python AI workflow service, which calls Foundry.</summary>
    AiWorkflow = 1,

    /// <summary>The API calls the Foundry project itself, with its own Entra identity.</summary>
    FoundryDirect = 2,
}

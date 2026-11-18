namespace CareLoop.Api.Infrastructure.Ai;

/// <summary>
/// AI settings, bound to the <c>Ai</c> configuration section.
/// </summary>
/// <remarks>
/// Two routes to a model are supported and both sit behind <c>IAiProvider</c>: the API can call
/// the Python AI workflow service, which owns the prompt and calls Foundry (the agreed route,
/// decision 8), or it can call a Foundry deployment directly. The second exists so the summary
/// still works when the Python service is not running, and so the two can be compared.
/// </remarks>
public sealed class AiOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "Ai";

    /// <summary>Longest wait the dashboard's agents accept for one call, in seconds.</summary>
    public const int MaximumTimeoutSeconds = 300;

    /// <summary>Which provider to use: <c>NoOp</c> (default, AI off), <c>AiWorkflow</c> or <c>FoundryDirect</c>.</summary>
    /// <remarks>It chooses the route for the handoff summary and for the dashboard's agents alike (<see cref="Route"/>).</remarks>
    public string Provider { get; set; } = "NoOp";

    /// <summary>Whether a draft is produced automatically when a handoff is proposed (decision 11).</summary>
    /// <remarks>Off by default: an automatic draft spends a model call and sends facts out of CareLoop with nobody having asked, so switching it on is a deliberate choice.</remarks>
    public bool AutoDraftOnHandoff { get; set; }

    /// <summary>How long to wait for a draft, or for one of the dashboard's agents (token included), before giving up and reporting it unavailable.</summary>
    /// <remarks>Bounded so a slow model cannot hold a request open; the person is told no draft is available rather than being left waiting.</remarks>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Largest request, in bytes, that may be sent to a provider: a handoff fact set, or one agent request exactly as it would be sent.</summary>
    /// <remarks>A request above this is refused before any call. It means the fact set grew beyond what was designed for, which is worth a person's attention rather than a truncated prompt.</remarks>
    public int MaxRequestBytes { get; set; } = 32_768;

    /// <summary>Version of the prompt and template to ask the provider for, recorded with every draft.</summary>
    public string PromptVersion { get; set; } = "fr11-v1";

    /// <summary>Settings for calling the Python AI workflow service.</summary>
    public AiWorkflowOptions AiWorkflow { get; set; } = new();

    /// <summary>Settings for calling a Foundry deployment directly from the API.</summary>
    public FoundryOptions FoundryDirect { get; set; } = new();

    /// <summary>Reads <see cref="Provider"/> as a route.</summary>
    /// <returns>The route; <see cref="AiRoute.NoOp"/> for an empty or unknown value, as the handoff summary reads it.</returns>
    public AiRoute Route() => RouteOf(Provider);

    /// <summary>Reads an <c>Ai:Provider</c> value as a route.</summary>
    /// <param name="provider">The setting, compared without regard to case.</param>
    /// <returns><see cref="AiRoute.AiWorkflow"/>, <see cref="AiRoute.FoundryDirect"/>, or <see cref="AiRoute.NoOp"/> for anything else.</returns>
    public static AiRoute RouteOf(string? provider) =>
        string.Equals(provider?.Trim(), nameof(AiRoute.AiWorkflow), StringComparison.OrdinalIgnoreCase) ? AiRoute.AiWorkflow
        : string.Equals(provider?.Trim(), nameof(AiRoute.FoundryDirect), StringComparison.OrdinalIgnoreCase) ? AiRoute.FoundryDirect
        : AiRoute.NoOp;
}

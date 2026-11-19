namespace CareLoop.Api.Infrastructure.Ai;

/// <summary>
/// AI settings, bound to the <c>Ai</c> configuration section.
/// </summary>
/// <remarks>
/// The no-op provider is the default. A direct Foundry provider remains a deferred option behind
/// <c>IAiProvider</c>; no external AI workflow service is currently included.
/// </remarks>
public sealed class AiOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "Ai";

    /// <summary>Which provider to use: <c>NoOp</c> (default, AI off) or the deferred <c>FoundryDirect</c> provider.</summary>
    public string Provider { get; set; } = "NoOp";

    /// <summary>Whether a draft is produced automatically when a handoff is proposed (decision 11).</summary>
    /// <remarks>Off by default: an automatic draft spends a model call and sends facts out of CareLoop with nobody having asked, so switching it on is a deliberate choice.</remarks>
    public bool AutoDraftOnHandoff { get; set; }

    /// <summary>How long to wait for a draft before giving up and reporting it unavailable.</summary>
    /// <remarks>Bounded so a slow model cannot hold a request open; the person is told no draft is available rather than being left waiting.</remarks>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Largest request, in bytes, that may be sent to a provider.</summary>
    /// <remarks>A request above this is refused before any call. It means the fact set grew beyond what was designed for, which is worth a person's attention rather than a truncated prompt.</remarks>
    public int MaxRequestBytes { get; set; } = 32_768;

    /// <summary>Version of the prompt and template to ask the provider for, recorded with every draft.</summary>
    public string PromptVersion { get; set; } = "fr11-v1";

    /// <summary>Settings for calling a Foundry deployment directly from the API.</summary>
    public FoundryOptions FoundryDirect { get; set; } = new();
}

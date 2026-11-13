namespace CareLoop.Api.Features.Notifications;

/// <summary>
/// Renders approved message templates.
/// </summary>
/// <remarks>
/// Message wording is approved content, so it lives in files under
/// <c>Infrastructure/Templates/{language}/</c> rather than in code. Each template has a standard
/// variant and an SMS-safe variant (<c>{templateKey}.txt</c> and <c>{templateKey}.sms.txt</c>).
/// Everything is language-keyed so Arabic can be added later without code changes.
/// </remarks>
public sealed class TemplateRenderer
{
    private readonly IHostEnvironment _environment;

    /// <summary>
    /// Creates the renderer.
    /// </summary>
    /// <param name="environment">Locates the template files under the application's content root.</param>
    public TemplateRenderer(IHostEnvironment environment)
    {
        _environment = environment;
    }

    /// <summary>
    /// Renders a template for one language and channel class.
    /// </summary>
    /// <param name="templateKey">Key of the template, for example <c>patient-result-ready</c>.</param>
    /// <param name="language">BCP 47 language tag; falls back to English when the language has no template.</param>
    /// <param name="smsSafe"><see langword="true"/> to use the SMS-safe variant, which carries no sensitive content.</param>
    /// <param name="values">Placeholder values, keyed by placeholder name.</param>
    /// <returns>The rendered subject and body.</returns>
    /// <exception cref="FileNotFoundException">No template with that key exists for the language or for English.</exception>
    /// <exception cref="KeyNotFoundException">The template uses a placeholder that <paramref name="values"/> does not supply.</exception>
    public RenderedTemplate Render(string templateKey, string language, bool smsSafe, IReadOnlyDictionary<string, string> values)
    {
        // TODO(WS-4): load {language}/{templateKey}[.sms].txt, substitute {{placeholders}}, fall back to en.
        throw new NotImplementedException("WS4: TemplateRenderer.Render");
    }
}

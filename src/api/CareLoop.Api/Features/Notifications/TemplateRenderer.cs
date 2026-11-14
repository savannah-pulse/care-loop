using System.Text;
using System.Text.RegularExpressions;

namespace CareLoop.Api.Features.Notifications;

/// <summary>
/// Renders approved message templates.
/// </summary>
/// <remarks>
/// <para>
/// Message wording is approved content, so it lives in files under
/// <c>Infrastructure/Templates/{language}/</c> rather than in code. Each template has a standard
/// variant and an SMS-safe variant (<c>{templateKey}.txt</c> and <c>{templateKey}.sms.txt</c>).
/// Everything is language-keyed so Arabic can be added later without code changes.
/// </para>
/// <para>
/// File format: an optional <c>Subject:</c> line before the first blank line becomes the subject;
/// every other line is the body, so a placeholder-wording marker stays visible in the message.
/// Placeholders are written <c>{{name}}</c>.
/// </para>
/// </remarks>
public sealed partial class TemplateRenderer
{
    /// <summary>The language used when a template does not exist in the requested one.</summary>
    public const string FallbackLanguage = "en";

    private const string SubjectPrefix = "Subject:";

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
    /// <param name="language">BCP 47 language tag; a regional tag (<c>en-GB</c>) also tries its primary language, and anything without a template falls back to English.</param>
    /// <param name="smsSafe"><see langword="true"/> to use the SMS-safe variant, which carries no sensitive content.</param>
    /// <param name="values">Placeholder values, keyed by placeholder name.</param>
    /// <returns>The rendered subject and body.</returns>
    /// <exception cref="ArgumentException"><paramref name="templateKey"/> or <paramref name="language"/> is not a well-formed key or tag.</exception>
    /// <exception cref="FileNotFoundException">No template with that key exists for the language or for English.</exception>
    /// <exception cref="KeyNotFoundException">The template uses a placeholder that <paramref name="values"/> does not supply.</exception>
    public RenderedTemplate Render(string templateKey, string language, bool smsSafe, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (string.IsNullOrWhiteSpace(templateKey) || !TemplateKeyPattern().IsMatch(templateKey))
        {
            throw new ArgumentException($"'{templateKey}' is not a template key.", nameof(templateKey));
        }

        if (string.IsNullOrWhiteSpace(language) || !LanguageTagPattern().IsMatch(language))
        {
            throw new ArgumentException($"'{language}' is not a BCP 47 language tag.", nameof(language));
        }

        var fileName = smsSafe ? $"{templateKey}.sms.txt" : $"{templateKey}.txt";
        var path = Candidates(language).Select(l => Path.Combine(TemplatesDirectory(), l, fileName)).FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException($"No '{fileName}' template exists for '{language}' or '{FallbackLanguage}'.", fileName);

        var (subject, body) = Split(File.ReadAllText(path, Encoding.UTF8));
        return new RenderedTemplate
        {
            Subject = subject is null ? null : Substitute(subject, values, templateKey).Trim(),
            // Trimmed after substitution, so an empty optional line at the end leaves nothing behind.
            Body = Substitute(body, values, templateKey).Trim(),
            IsSmsSafe = smsSafe,
        };
    }

    private string TemplatesDirectory()
    {
        var underContentRoot = Path.Combine(_environment.ContentRootPath, "Infrastructure", "Templates");
        return Directory.Exists(underContentRoot)
            ? underContentRoot
            : Path.Combine(AppContext.BaseDirectory, "Infrastructure", "Templates");
    }

    private static IEnumerable<string> Candidates(string language)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var primary = language.Split('-')[0];
        foreach (var candidate in new[] { language, primary, FallbackLanguage })
        {
            if (seen.Add(candidate))
            {
                yield return candidate;
            }
        }
    }

    private static (string? Subject, string Body) Split(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        string? subject = null;
        for (var i = 0; i < lines.Count && lines[i].Trim().Length > 0; i++)
        {
            if (lines[i].StartsWith(SubjectPrefix, StringComparison.OrdinalIgnoreCase))
            {
                subject = lines[i][SubjectPrefix.Length..].Trim();
                lines.RemoveAt(i);
                if (i < lines.Count && lines[i].Trim().Length == 0)
                {
                    lines.RemoveAt(i);
                }

                break;
            }
        }

        return (subject, string.Join('\n', lines).Trim());
    }

    private static string Substitute(string text, IReadOnlyDictionary<string, string> values, string templateKey) =>
        PlaceholderPattern().Replace(text, match =>
        {
            var name = match.Groups[1].Value;
            return values.TryGetValue(name, out var value)
                ? value
                : throw new KeyNotFoundException($"Template '{templateKey}' needs a value for '{{{{{name}}}}}'.");
        });

    [GeneratedRegex(@"\{\{\s*([A-Za-z0-9_.-]+)\s*\}\}")]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex TemplateKeyPattern();

    [GeneratedRegex("^[A-Za-z]{2,8}(-[A-Za-z0-9]{1,8})*$")]
    private static partial Regex LanguageTagPattern();
}

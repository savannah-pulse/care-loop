namespace CareLoop.Api.Features.Notifications;

/// <summary>
/// A message template rendered with its values.
/// </summary>
public sealed record RenderedTemplate
{
    /// <summary>The rendered subject line, when the template has one.</summary>
    public string? Subject { get; init; }

    /// <summary>The rendered body.</summary>
    public required string Body { get; init; }

    /// <summary><see langword="true"/> when the SMS-safe variant of the template was used.</summary>
    public required bool IsSmsSafe { get; init; }
}

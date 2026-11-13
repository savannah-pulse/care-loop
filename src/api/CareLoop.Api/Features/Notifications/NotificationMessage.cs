using CareLoop.Domain.Recipients;

namespace CareLoop.Api.Features.Notifications;

/// <summary>
/// One rendered message addressed to one recipient on one channel.
/// </summary>
public sealed record NotificationMessage
{
    /// <summary>Identifier of the notification attempt this message belongs to; delivery reports refer to it.</summary>
    public required Guid AttemptId { get; init; }

    /// <summary>The case the message is about.</summary>
    public required Guid CaseId { get; init; }

    /// <summary>Who the message is for.</summary>
    public required Recipient Recipient { get; init; }

    /// <summary>The contact point on this provider's channel that the message is sent to.</summary>
    public required ContactPoint ContactPoint { get; init; }

    /// <summary>Key of the approved template the message was rendered from.</summary>
    public required string TemplateKey { get; init; }

    /// <summary>The rendered subject line, for channels that have one.</summary>
    public string? Subject { get; init; }

    /// <summary>The rendered message body. For staff it carries no patient detail; for SMS it is the SMS-safe text.</summary>
    public required string Body { get; init; }

    /// <summary><see langword="true"/> when the SMS-safe template was used.</summary>
    public required bool IsSmsSafe { get; init; }
}

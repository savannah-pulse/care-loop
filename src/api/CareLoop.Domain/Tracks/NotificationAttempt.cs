using CareLoop.Domain.Recipients;

namespace CareLoop.Domain.Tracks;

/// <summary>
/// One send of a notification on one channel.
/// </summary>
/// <remarks>
/// Each channel's send is recorded separately so the notification can be judged delivered if any
/// channel delivers and failed only if all fail, and so the audit trail shows exactly what was tried.
/// </remarks>
public sealed record NotificationAttempt
{
    /// <summary>Identifier of the attempt; delivery reports refer to it.</summary>
    public required Guid Id { get; init; }

    /// <summary>The channel the message was sent on.</summary>
    public required NotificationChannel Channel { get; init; }

    /// <summary>When the send was made.</summary>
    public required DateTimeOffset AttemptedAt { get; init; }

    /// <summary>The latest known outcome of the attempt.</summary>
    public required NotificationAttemptStatus Status { get; init; }

    /// <summary><see langword="true"/> when the SMS-safe template was used because the channel does not allow sensitive content.</summary>
    public required bool UsedSmsSafeTemplate { get; init; }

    /// <summary>The provider's own message identifier, when it returned one.</summary>
    public string? ProviderMessageId { get; init; }

    /// <summary>The failure reason, when the attempt failed.</summary>
    public string? FailureReason { get; init; }
}

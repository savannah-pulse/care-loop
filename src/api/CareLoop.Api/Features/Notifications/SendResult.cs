namespace CareLoop.Api.Features.Notifications;

/// <summary>
/// The outcome of one send on one channel.
/// </summary>
public sealed record SendResult
{
    /// <summary><see langword="true"/> when the provider accepted the message. Acceptance is not delivery, and delivery is not acknowledgement.</summary>
    public required bool Accepted { get; init; }

    /// <summary>The provider's own message identifier, when it returned one.</summary>
    public string? ProviderMessageId { get; init; }

    /// <summary>Why the send failed, when it was not accepted.</summary>
    public string? FailureReason { get; init; }
}

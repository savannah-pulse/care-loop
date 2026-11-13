using CareLoop.Domain.Recipients;

namespace CareLoop.Api.Features.Notifications;

/// <summary>
/// One channel over which a notification can be sent.
/// </summary>
/// <remarks>
/// This is the seam that lets real SMS and email delivery be added later without touching the
/// sender or the router. Providers are enabled through configuration, not chosen per recipient.
/// </remarks>
public interface INotificationProvider
{
    /// <summary>The channel this provider sends on; also the name used in <see cref="NotificationOptions.EnabledProviders"/>.</summary>
    NotificationChannel Channel { get; }

    /// <summary><see langword="false"/> when the channel must not carry sensitive content (SMS), in which case the sender uses the SMS-safe template.</summary>
    bool AllowsSensitiveContent { get; }

    /// <summary>
    /// Tells whether this provider can reach a recipient.
    /// </summary>
    /// <param name="recipient">The recipient to check.</param>
    /// <returns><see langword="true"/> when the recipient has a contact point for <see cref="Channel"/>.</returns>
    bool CanReach(Recipient recipient);

    /// <summary>
    /// Sends one message.
    /// </summary>
    /// <param name="message">The rendered message for this channel.</param>
    /// <param name="ct">Cancels the send.</param>
    /// <returns>Whether the provider accepted the message, with its message identifier or the failure reason.</returns>
    /// <remarks>A provider reports failure through the result rather than by throwing, so one failing channel does not stop the others.</remarks>
    Task<SendResult> SendAsync(NotificationMessage message, CancellationToken ct);
}

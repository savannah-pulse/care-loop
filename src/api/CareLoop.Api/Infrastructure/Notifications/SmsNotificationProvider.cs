using CareLoop.Api.Features.Notifications;
using CareLoop.Domain.Recipients;

namespace CareLoop.Api.Infrastructure.Notifications;

/// <summary>
/// Production SMS provider. Deliberately not built yet.
/// </summary>
/// <remarks>
/// Real SMS delivery is not built yet; the prototype sends through the simulator provider
/// only. This class exists so the seam is in place: it can be implemented later and
/// switched on through <see cref="NotificationOptions.EnabledProviders"/> without touching the
/// sender or the router. Until then it must stay out of the enabled providers; using it throws.
/// </remarks>
public sealed class SmsNotificationProvider : INotificationProvider
{
    /// <inheritdoc/>
    public NotificationChannel Channel => NotificationChannel.Sms;

    /// <inheritdoc/>
    public bool AllowsSensitiveContent => false;

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">Always: the provider is not built yet.</exception>
    public bool CanReach(Recipient recipient) =>
        throw new NotSupportedException("SmsNotificationProvider is not built yet; only the Simulator provider may be enabled.");

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">Always: the provider is not built yet.</exception>
    public Task<SendResult> SendAsync(NotificationMessage message, CancellationToken ct) =>
        throw new NotSupportedException("SmsNotificationProvider is not built yet; only the Simulator provider may be enabled.");
}

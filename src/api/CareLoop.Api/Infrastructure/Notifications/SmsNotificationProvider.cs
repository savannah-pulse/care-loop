using CareLoop.Domain.Recipients;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Infrastructure.Notifications;

/// <summary>
/// Sends text messages to the simulator configured in <c>Notifications:Sms</c>.
/// </summary>
/// <remarks>
/// A text message must not carry sensitive content, so the sender gives this provider the SMS template
/// (<c>{key}.sms.txt</c>). It has no subject line.
/// </remarks>
public sealed class SmsNotificationProvider : SimulatorNotificationProvider
{
    /// <summary>
    /// Creates the provider.
    /// </summary>
    /// <param name="http">The HTTP client supplied by the HTTP client factory.</param>
    /// <param name="options">The simulator settings, one named set per channel.</param>
    public SmsNotificationProvider(HttpClient http, IOptionsMonitor<SimulatorNotificationOptions> options)
        : base(http, options)
    {
    }

    /// <inheritdoc/>
    public override NotificationChannel Channel => NotificationChannel.Sms;

    /// <inheritdoc/>
    public override bool AllowsSensitiveContent => false;
}

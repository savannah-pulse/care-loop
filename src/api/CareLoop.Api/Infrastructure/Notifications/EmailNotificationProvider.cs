using CareLoop.Domain.Recipients;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Infrastructure.Notifications;

/// <summary>
/// Sends email to the simulator configured in <c>Notifications:Email</c>.
/// </summary>
/// <remarks>
/// Email receives the standard template (<c>{key}.txt</c>), with its subject line. For now the standard
/// templates say no more than the SMS ones: neither carries a result, because there is no secure place for a
/// patient to read one, and a person always gives it.
/// </remarks>
public sealed class EmailNotificationProvider : SimulatorNotificationProvider
{
    /// <summary>
    /// Creates the provider.
    /// </summary>
    /// <param name="http">The HTTP client supplied by the HTTP client factory.</param>
    /// <param name="options">The simulator settings, one named set per channel.</param>
    public EmailNotificationProvider(HttpClient http, IOptionsMonitor<SimulatorNotificationOptions> options)
        : base(http, options)
    {
    }

    /// <inheritdoc/>
    public override NotificationChannel Channel => NotificationChannel.Email;

    /// <inheritdoc/>
    public override bool AllowsSensitiveContent => true;
}

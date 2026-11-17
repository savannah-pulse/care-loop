namespace CareLoop.Api.Infrastructure.Notifications;

/// <summary>
/// Where the simulator of one channel is reached and how its callbacks are trusted. Bound once per channel:
/// <c>Notifications:Sms</c> for text messages and <c>Notifications:Email</c> for email.
/// </summary>
/// <remarks>
/// These settings belong to the provider of that channel alone. In the prototype both sections point at
/// the messaging simulator, which stands in for a real SMS and email service; nothing else in CareLoop knows
/// the simulator exists.
/// </remarks>
public sealed class SimulatorNotificationOptions
{
    /// <summary>Name of the configuration section that holds one section per channel.</summary>
    public const string ParentSectionName = "Notifications";

    /// <summary>Base URL of the simulator. Messages are posted to <c>{BaseUrl}/messages</c>.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// The secret shared with the simulator, with which it signs what it posts to
    /// <c>POST /notifications/inbound/{channel}</c>.
    /// </summary>
    /// <remarks>
    /// A secret, so it comes from the environment (for example <c>Notifications__Sms__InboundSecret</c>) or a secret
    /// store and is never committed. While it is not set, every inbound message on that channel is refused: an
    /// unsigned reply could mark a patient as notified or acknowledged without the patient having seen anything.
    /// </remarks>
    public string? InboundSecret { get; set; }

    /// <summary>
    /// The configuration section of one channel.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <returns><c>Notifications:Sms</c> or <c>Notifications:Email</c>.</returns>
    public static string SectionFor(CareLoop.Domain.Recipients.NotificationChannel channel) => $"{ParentSectionName}:{channel}";
}

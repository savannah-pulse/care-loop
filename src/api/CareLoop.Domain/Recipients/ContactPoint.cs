namespace CareLoop.Domain.Recipients;

/// <summary>
/// One way of reaching a <see cref="Recipient"/> on a specific <see cref="NotificationChannel"/>.
/// </summary>
/// <remarks>
/// A contact point is personal data. It is resolved when a notification is sent and is not
/// copied into case state. While only the simulators are connected, every address is a
/// synthetic one.
/// </remarks>
public sealed record ContactPoint
{
    /// <summary>The channel this contact point belongs to.</summary>
    public required NotificationChannel Channel { get; init; }

    /// <summary>The channel-specific address, a phone number or an email address.</summary>
    public required string Address { get; init; }
}

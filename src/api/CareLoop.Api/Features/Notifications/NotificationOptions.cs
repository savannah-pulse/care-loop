namespace CareLoop.Api.Features.Notifications;

/// <summary>
/// Which notification providers are switched on, bound to the <c>Notifications</c> configuration section.
/// </summary>
/// <remarks>
/// Channels are system configuration, not recipient preference. The providers are text message
/// (<c>Sms</c>) and email (<c>Email</c>); each has its own settings section beside this one.
/// </remarks>
public sealed class NotificationOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "Notifications";

    /// <summary>Names of the enabled providers, matching <see cref="CareLoop.Domain.Recipients.NotificationChannel"/> member names. A message is sent on every enabled provider that can reach the recipient.</summary>
    public List<string> EnabledProviders { get; set; } = [];

    /// <summary>Optional per-domain-event override of the enabled providers, keyed by domain event name (for example <c>EscalationRaised</c>).</summary>
    public Dictionary<string, List<string>> PerEventOverrides { get; set; } = [];

    /// <summary>
    /// Checks that every provider named is a notification channel.
    /// </summary>
    /// <param name="options">The options to check.</param>
    /// <returns><see langword="false"/> when a name in <see cref="EnabledProviders"/> or <see cref="PerEventOverrides"/> is not a member of <see cref="CareLoop.Domain.Recipients.NotificationChannel"/>.</returns>
    /// <remarks>A misspelt or retired name would otherwise be ignored, and its messages silently not sent.</remarks>
    public static bool NamesOnlyKnownProviders(NotificationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.EnabledProviders.Concat(options.PerEventOverrides.Values.SelectMany(v => v)).All(IsKnown);
    }

    /// <summary>
    /// Tells whether a channel is switched on for any message: named in <see cref="EnabledProviders"/> or in any
    /// <see cref="PerEventOverrides"/> entry.
    /// </summary>
    /// <param name="channel">The channel to check.</param>
    /// <returns><see langword="true"/> when some message may be sent on it, so its delivery reports and replies must be heard.</returns>
    public bool IsUsed(CareLoop.Domain.Recipients.NotificationChannel channel) =>
        EnabledProviders.Concat(PerEventOverrides.Values.SelectMany(v => v))
            .Contains(channel.ToString(), StringComparer.OrdinalIgnoreCase);

    // Exactly a channel's name, ignoring case only, as the sender and the inbound endpoint compare it:
    // Enum.TryParse would also accept " Sms " or "Sms, Email", which then match no provider.
    private static bool IsKnown(string name) =>
        Enum.GetNames<CareLoop.Domain.Recipients.NotificationChannel>().Contains(name, StringComparer.OrdinalIgnoreCase);
}

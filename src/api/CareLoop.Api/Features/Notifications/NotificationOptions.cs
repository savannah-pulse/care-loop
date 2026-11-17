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
    /// The time zone messages give dates and times in, as an IANA identifier such as <c>Asia/Dubai</c>; UTC when not set.
    /// </summary>
    /// <remarks>
    /// People read an appointment or an acknowledgement time as local time, so a message never shows UTC to a patient,
    /// a caregiver or a member of staff unless the deployment is set to it. Checked at start-up (<see cref="HasKnownTimeZone"/>).
    /// </remarks>
    public string? TimeZone { get; set; }

    /// <summary>
    /// The names staff messages give facilities, keyed by facility identifier (for example <c>family-health-centre</c> to
    /// <c>Family Health Centre</c>). A facility with no entry is named by its identifier in words.
    /// </summary>
    public Dictionary<string, string> FacilityNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Checks that <see cref="TimeZone"/>, when set, names a time zone this host knows.
    /// </summary>
    /// <param name="options">The options to check.</param>
    /// <returns><see langword="false"/> when the time zone is set but unknown.</returns>
    public static bool HasKnownTimeZone(NotificationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return string.IsNullOrWhiteSpace(options.TimeZone) || TimeZoneInfo.TryFindSystemTimeZoneById(options.TimeZone.Trim(), out _);
    }

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

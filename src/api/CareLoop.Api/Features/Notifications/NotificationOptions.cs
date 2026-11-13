namespace CareLoop.Api.Features.Notifications;

/// <summary>
/// Which notification providers are switched on, bound to the <c>Notifications</c> configuration section.
/// </summary>
/// <remarks>
/// Channels are system configuration, not recipient preference. Only the simulator provider is
/// enabled in the prototype; SMS and email are switched on here once they are built.
/// </remarks>
public sealed class NotificationOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "Notifications";

    /// <summary>Names of the enabled providers, matching <see cref="CareLoop.Domain.Recipients.NotificationChannel"/> member names. A message is sent on every enabled provider that can reach the recipient.</summary>
    public List<string> EnabledProviders { get; set; } = [];

    /// <summary>Optional per-domain-event override of the enabled providers, keyed by domain event name (for example <c>EscalationRaised</c>).</summary>
    public Dictionary<string, List<string>> PerEventOverrides { get; set; } = [];
}

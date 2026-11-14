namespace CareLoop.Api.Features.Outbox;

/// <summary>
/// Settings for the outbox dispatcher, bound to the <c>Outbox</c> configuration section.
/// </summary>
public sealed class OutboxOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "Outbox";

    /// <summary>Whether the dispatcher background service runs. On in <c>appsettings.json</c>; a test host turns it off.</summary>
    public bool Enabled { get; set; }

    /// <summary>How often the dispatcher looks for undelivered messages.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>How many times a delivery is attempted for one subscriber before it is parked for a person to look at.</summary>
    public int MaxAttempts { get; set; } = 10;

    /// <summary>
    /// How long one instance's claim on a delivery lasts. Longer than any subscriber takes, so a slow delivery is not
    /// taken over while still running; short enough that a delivery left by a stopped instance is picked up again soon.
    /// </summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(5);
}

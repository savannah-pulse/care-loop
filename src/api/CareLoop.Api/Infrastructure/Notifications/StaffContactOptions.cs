using CareLoop.Domain.Recipients;

namespace CareLoop.Api.Infrastructure.Notifications;

/// <summary>
/// How staff are reached, bound to the <c>Notifications:StaffContacts</c> configuration section: one entry
/// per role, keyed by the role's name.
/// </summary>
/// <remarks>
/// <para>
/// A staff notification goes to a role or a queue and carries no patient detail ("a case needs your action"),
/// so its address is system configuration, not something the hospital system sends with each owner. A role
/// with no entry falls back to the entry named <see cref="DefaultKey"/>; with neither, that role cannot be
/// reached and the notification is retried, never dropped silently.
/// </para>
/// <para>
/// Example: <c>"StaffContacts": { "nurse-navigator": { "Channel": "Email", "Address": "…" }, "default": { … } }</c>.
/// </para>
/// </remarks>
public sealed class StaffContactOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "Notifications:StaffContacts";

    /// <summary>The key of the entry used for a role that has none of its own.</summary>
    public const string DefaultKey = "default";

    /// <summary>The contact of each role, keyed by role name without regard to case.</summary>
    public Dictionary<string, StaffContact> ByRole { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Finds how a role is reached.
    /// </summary>
    /// <param name="role">The role of the owner to notify.</param>
    /// <returns>The role's contact, the default contact, or <see langword="null"/> when neither is usable.</returns>
    public StaffContact? For(string role)
    {
        if (!string.IsNullOrWhiteSpace(role) && ByRole.TryGetValue(role, out var own) && own.IsUsable)
        {
            return own;
        }

        return ByRole.TryGetValue(DefaultKey, out var fallback) && fallback.IsUsable ? fallback : null;
    }
}

/// <summary>
/// The channel and address a staff role is notified on.
/// </summary>
public sealed class StaffContact
{
    /// <summary>The channel: <c>Sms</c> or <c>Email</c>.</summary>
    public NotificationChannel? Channel { get; set; }

    /// <summary>The phone number or email address of the role or its shared queue.</summary>
    public string? Address { get; set; }

    /// <summary><see langword="true"/> when both a known channel and an address are set.</summary>
    public bool IsUsable => Channel is { } channel && Enum.IsDefined(channel) && !string.IsNullOrWhiteSpace(Address);
}

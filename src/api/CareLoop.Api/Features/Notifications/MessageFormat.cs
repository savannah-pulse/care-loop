using System.Globalization;

namespace CareLoop.Api.Features.Notifications;

/// <summary>
/// Writes dates, times, facilities and roles the way a message to a person gives them.
/// </summary>
/// <remarks>
/// Messages are read by people, not systems: a time is local (<see cref="NotificationOptions.TimeZone"/>), a date names
/// the day ("Tuesday 24 November 2026"), a facility has its name and a role is in words ("the facility lead"). English
/// only for now, like the templates; the formats are invariant so a server's culture never changes a message.
/// </remarks>
public sealed class MessageFormat
{
    private readonly TimeZoneInfo _zone;
    /// <summary>What a patient message says in place of a facility's contact when none is configured.</summary>
    public const string NoFacilityContact = "the clinic";

    private readonly IReadOnlyDictionary<string, string> _facilities;
    private readonly IReadOnlyDictionary<string, string> _contacts;

    /// <summary>
    /// Creates the formatter.
    /// </summary>
    /// <param name="options">Supplies the time zone and the facility names.</param>
    /// <exception cref="TimeZoneNotFoundException">The configured time zone is not known on this host.</exception>
    public MessageFormat(NotificationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _zone = string.IsNullOrWhiteSpace(options.TimeZone) ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(options.TimeZone.Trim());
        _facilities = new Dictionary<string, string>(options.FacilityNames, StringComparer.OrdinalIgnoreCase);
        _contacts = new Dictionary<string, string>(options.FacilityContacts, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>A formatter in UTC with no facility names, for tests and tools.</summary>
    public static MessageFormat Default { get; } = new(new NotificationOptions());

    /// <summary>Gives an instant as local time of day.</summary>
    /// <param name="at">The instant.</param>
    /// <returns>For example <c>13:30</c>.</returns>
    public string Time(DateTimeOffset at) => Local(at).ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Gives an instant as the local date with its day.</summary>
    /// <param name="at">The instant.</param>
    /// <returns>For example <c>Tuesday 24 November 2026</c>.</returns>
    public string Date(DateTimeOffset at) => Local(at).ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>Gives an instant as the local day without the year, for a recent event.</summary>
    /// <param name="at">The instant.</param>
    /// <returns>For example <c>Tuesday 17 November</c>.</returns>
    public string Day(DateTimeOffset at) => Local(at).ToString("dddd d MMMM", CultureInfo.InvariantCulture);

    /// <summary>Names a facility.</summary>
    /// <param name="facilityId">The facility identifier.</param>
    /// <returns>Its configured name, or the identifier in words (<c>general-hospital</c> becomes <c>General Hospital</c>).</returns>
    public string Facility(string facilityId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(facilityId);
        return _facilities.TryGetValue(facilityId.Trim(), out var name) && !string.IsNullOrWhiteSpace(name)
            ? name.Trim()
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(Words(facilityId));
    }

    /// <summary>Gives how a patient contacts a facility.</summary>
    /// <param name="facilityId">The facility identifier.</param>
    /// <returns>Its configured contact (<see cref="NotificationOptions.FacilityContacts"/>), or <see langword="null"/> when none is configured.</returns>
    public string? FacilityContact(string facilityId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(facilityId);
        return _contacts.TryGetValue(facilityId.Trim(), out var contact) && !string.IsNullOrWhiteSpace(contact) ? contact.Trim() : null;
    }

    /// <summary>Names a role in words, for the middle of a sentence.</summary>
    /// <param name="role">The role key, such as <c>facility-lead</c>.</param>
    /// <returns>For example <c>facility lead</c>.</returns>
    public static string Role(string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        return Words(role);
    }

    private DateTime Local(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, _zone).DateTime;

    private static string Words(string key) => key.Trim().Replace('-', ' ').Replace('_', ' ').ToLowerInvariant();
}

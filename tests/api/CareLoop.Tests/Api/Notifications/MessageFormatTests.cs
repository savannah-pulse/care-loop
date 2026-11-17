using CareLoop.Api.Features.Notifications;

namespace CareLoop.Tests.Api.Notifications;

public sealed class MessageFormatTests
{
    private static readonly DateTimeOffset NineThirtyUtc = new(2026, 11, 24, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Dates_and_times_are_local_and_name_the_day()
    {
        var format = new MessageFormat(new NotificationOptions { TimeZone = "Asia/Dubai" });

        Assert.Equal("13:30", format.Time(NineThirtyUtc));
        Assert.Equal("Tuesday 24 November 2026", format.Date(NineThirtyUtc));
        Assert.Equal("Tuesday 24 November", format.Day(NineThirtyUtc));
    }

    [Fact]
    public void Without_a_time_zone_messages_use_UTC()
    {
        Assert.Equal("09:30", MessageFormat.Default.Time(NineThirtyUtc));
    }

    [Fact]
    public void A_facility_has_its_configured_name_or_its_identifier_in_words()
    {
        var format = new MessageFormat(new NotificationOptions { FacilityNames = new(StringComparer.OrdinalIgnoreCase) { ["family-health-centre"] = "Family Health Centre" } });

        Assert.Equal("Family Health Centre", format.Facility("FAMILY-HEALTH-CENTRE"));
        Assert.Equal("General Hospital", format.Facility("general-hospital"));
        Assert.Equal("facility lead", MessageFormat.Role("facility-lead"));
    }

    [Theory]
    [InlineData("Asia/Dubai", true)]
    [InlineData(null, true)]
    [InlineData("Mars/Olympus", false)]
    public void An_unknown_time_zone_is_refused_at_start_up(string? zone, bool valid) =>
        Assert.Equal(valid, NotificationOptions.HasKnownTimeZone(new NotificationOptions { TimeZone = zone }));
}

using static CareLoop.Tests.Api.Notifications.NotificationTestSupport;

namespace CareLoop.Tests.Api.Notifications;

/// <summary>
/// Templates are language-keyed files with a standard and an SMS-safe variant.
/// </summary>
public sealed class TemplateRendererTests
{
    private static readonly Dictionary<string, string> Values = new() { ["caseReference"] = "AB12CD34", ["facility"] = "SYN Family Health Centre", ["facilityContact"] = "SYN-PHONE-A", ["hisLink"] = "Task/SYN" };

    [Fact]
    public void Renders_placeholders()
    {
        var rendered = TemplatesIn(out _, ("en/greeting.txt", "Subject: Hello {{ caseReference }}\n\nYour reference is {{caseReference}}.\nOpen {{hisLink}}."))
            .Render("greeting", "en", smsSafe: false, Values);

        Assert.Equal("Hello AB12CD34", rendered.Subject);
        Assert.Equal("Your reference is AB12CD34.\nOpen Task/SYN.", rendered.Body);
        Assert.False(rendered.IsSmsSafe);
    }

    [Fact]
    public void Uses_the_sms_safe_variant_when_asked()
    {
        var renderer = ShippedTemplates();

        var standard = renderer.Render("patient-follow-up", "en", smsSafe: false, Values);
        var sms = renderer.Render("patient-follow-up", "en", smsSafe: true, Values);

        Assert.True(sms.IsSmsSafe);
        Assert.Null(sms.Subject);
        Assert.DoesNotContain('\n', sms.Body);
        Assert.Contains("Reference: AB12CD34", sms.Body);
        Assert.Equal("Your care team would like to speak with you", standard.Subject);
        Assert.Contains("Reference: AB12CD34", standard.Body);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Patient_follow_up_uses_the_care_team_callback_wording_with_the_facility_and_its_contact(bool smsSafe)
    {
        var body = ShippedTemplates().Render("patient-follow-up", "en", smsSafe, Values).Body;

        Assert.Contains("our care team at SYN Family Health Centre would like to speak with you.", body);
        Assert.Contains("Please contact us at SYN-PHONE-A when convenient.", body);
        Assert.Contains("Thank you.", body);
        Assert.DoesNotContain("PLACEHOLDER", body);
    }

    [Theory]
    [InlineData("patient-appointment", "Place: SYN Clinic", false)]
    [InlineData("patient-appointment", "Place: SYN Clinic", true)]
    [InlineData("patient-appointment-place-tbc", "Place: to be confirmed", false)]
    [InlineData("patient-appointment-place-tbc", "Place: to be confirmed", true)]
    public void Patient_appointment_uses_the_confirmation_wording_and_keeps_the_place_and_reply_codes(string templateKey, string place, bool smsSafe)
    {
        var values = new Dictionary<string, string>(Values) { ["appointmentDate"] = "Tuesday 24 November 2026", ["appointmentTime"] = "13:30", ["place"] = "SYN Clinic" };

        var body = ShippedTemplates().Render(templateKey, "en", smsSafe, values).Body;

        Assert.Contains("our appointment at SYN Family Health Centre is confirmed for Tuesday 24 November 2026 at 13:30.", body);
        Assert.Contains(place, body);
        Assert.Contains("If you need assistance or wish to change your appointment, please contact SYN-PHONE-A.", body);
        Assert.Contains("Reply 1 to confirm you got this message. Reply 2 if you would like your care team to call you.", body);
        Assert.Contains("Reference: AB12CD34", body);
        Assert.DoesNotContain("PLACEHOLDER", body);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Patient_message_asks_for_a_reply_and_points_at_no_app_portal_or_link(bool smsSafe)
    {
        var body = ShippedTemplates().Render("patient-follow-up", "en", smsSafe, Values).Body;

        Assert.Contains("Reply 1 to confirm you got this message", body);
        Assert.DoesNotContain("will come", body);
        Assert.Contains("Reply 2 if you would like your care team to call you", body);
        foreach (var notThere in new[] { "patient channel", "health app", "the app", "your app", "portal", "http", "www.", "log in", "open " })
        {
            Assert.DoesNotContain(notThere, body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("caregiver-appointment", "Place: SYN Clinic")]
    [InlineData("caregiver-appointment-place-tbc", "Place: to be confirmed")]
    public void Caregiver_text_message_says_the_same_as_the_email(string templateKey, string place)
    {
        var values = new Dictionary<string, string> { ["appointmentDate"] = "2026-11-17", ["appointmentTime"] = "09:30", ["place"] = "SYN Clinic" };

        foreach (var smsSafe in new[] { false, true })
        {
            var body = ShippedTemplates().Render(templateKey, "en", smsSafe, values).Body;
            Assert.Contains("2026-11-17", body);
            Assert.Contains("09:30", body);
            Assert.Contains(place, body);
            Assert.Contains("agreed that you may be told", body);
        }
    }

    [Fact]
    public void Falls_back_to_english_for_an_unknown_language()
    {
        var renderer = TemplatesIn(out _,
            ("en/greeting.txt", "Hello in English"),
            ("fr/greeting.txt", "Bonjour"));

        Assert.Equal("Hello in English", renderer.Render("greeting", "ar", smsSafe: false, Values).Body);
        Assert.Equal("Bonjour", renderer.Render("greeting", "fr-CA", smsSafe: false, Values).Body);
    }

    [Theory]
    [InlineData("EN")]
    [InlineData("EN-GB")]
    [InlineData("Ar-AE")]
    public void Upper_case_tags_still_reach_the_lower_case_folders(string language)
    {
        // The shipped folders are lower case; on a case-sensitive file system (Linux, Docker) "EN" is not "en".
        var rendered = ShippedTemplates().Render("patient-follow-up", language, smsSafe: false, Values);

        Assert.Contains("Reference: AB12CD34", rendered.Body);
    }

    [Fact]
    public void Missing_placeholder_value_throws()
    {
        var renderer = ShippedTemplates();

        Assert.Throws<KeyNotFoundException>(() => renderer.Render("staff-action-needed", "en", smsSafe: false, new Dictionary<string, string>()));
    }

    [Fact]
    public void Unknown_template_throws()
    {
        var renderer = ShippedTemplates();

        Assert.Throws<FileNotFoundException>(() => renderer.Render("no-such-template", "en", smsSafe: false, Values));
        Assert.Throws<ArgumentException>(() => renderer.Render("../en/patient-follow-up", "en", smsSafe: false, Values));
        Assert.Throws<ArgumentException>(() => renderer.Render("patient-follow-up", "../en", smsSafe: false, Values));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Staff_notice_shows_the_previous_acknowledgement_only_when_there_was_one(bool smsSafe)
    {
        var renderer = ShippedTemplates();
        const string Acknowledgement = "The facility lead acknowledged level 1 at 13:40 on Saturday 14 November.";

        var acknowledged = renderer.Render("staff-action-needed", "en", smsSafe, new Dictionary<string, string> { ["caseReference"] = "AB12CD34", ["facility"] = "Family Health Centre", ["previousAcknowledgement"] = Acknowledgement });
        var none = renderer.Render("staff-action-needed", "en", smsSafe, new Dictionary<string, string> { ["caseReference"] = "AB12CD34", ["facility"] = "Family Health Centre", ["previousAcknowledgement"] = string.Empty });

        Assert.Contains(Acknowledgement, acknowledged.Body);
        Assert.DoesNotContain("acknowledged", none.Body, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("A case at Family Health Centre needs your action. Open CareLoop and look for case AB12CD34.", none.Body);
        Assert.DoesNotContain("Task/", none.Body);
    }
}

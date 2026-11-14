using static CareLoop.Tests.Api.Notifications.NotificationTestSupport;

namespace CareLoop.Tests.Api.Notifications;

/// <summary>
/// Templates are language-keyed files with a standard and an SMS-safe variant.
/// </summary>
public sealed class TemplateRendererTests
{
    private static readonly Dictionary<string, string> Values = new() { ["caseReference"] = "AB12CD34", ["hisLink"] = "Task/SYN" };

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
        Assert.Contains("open the patient channel", sms.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AB12CD34", sms.Body);
        Assert.Equal("A follow-up step is waiting for you", standard.Subject);
        Assert.Contains("Reference: AB12CD34", standard.Body);
        Assert.Contains("[PLACEHOLDER WORDING, NOT APPROVED]", standard.Body);
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
        const string Acknowledgement = "Level 1 was acknowledged by facility-lead at 09:40 UTC on 2026-11-14.";

        var acknowledged = renderer.Render("staff-action-needed", "en", smsSafe, new Dictionary<string, string> { ["hisLink"] = "Task/SYN", ["previousAcknowledgement"] = Acknowledgement });
        var none = renderer.Render("staff-action-needed", "en", smsSafe, new Dictionary<string, string> { ["hisLink"] = "Task/SYN", ["previousAcknowledgement"] = string.Empty });

        Assert.Contains(Acknowledgement, acknowledged.Body);
        Assert.DoesNotContain("acknowledged", none.Body, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("Task/SYN", none.Body);
    }
}

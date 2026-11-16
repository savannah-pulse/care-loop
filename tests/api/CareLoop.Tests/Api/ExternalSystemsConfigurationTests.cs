using CareLoop.Api.Infrastructure.Fhir;
using CareLoop.Api.Infrastructure.Notifications;
using CareLoop.Domain.Recipients;
using Microsoft.Extensions.Configuration;

namespace CareLoop.Tests.Api;

/// <summary>
/// External systems bind as a named collection, and each channel's simulator and the staff contacts carry their
/// own settings, in both the settings-file shape and the environment-variable shape Docker Compose uses.
/// </summary>
public sealed class ExternalSystemsConfigurationTests
{
    [Fact]
    public void Base_settings_configure_no_external_system_no_simulator_address_and_no_staff_contact()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
            .Build();

        Assert.Empty(Systems(configuration).Systems);
        Assert.True(string.IsNullOrEmpty(Simulator(configuration, NotificationChannel.Sms).BaseUrl));
        Assert.True(string.IsNullOrEmpty(Simulator(configuration, NotificationChannel.Email).BaseUrl));
        Assert.True(string.IsNullOrEmpty(Simulator(configuration, NotificationChannel.Sms).InboundSecret));
        Assert.Null(StaffContacts(configuration).For("nurse-navigator"));
        Assert.Equal(["Sms", "Email"], configuration.GetSection("Notifications:EnabledProviders").Get<string[]>() ?? []);
    }

    [Fact]
    public void Development_settings_default_to_the_his_simulator()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.Development.json"))
            .Build();

        var system = Assert.Single(Systems(configuration).Systems);
        Assert.Equal("his-simulator", system.Key);
        Assert.Equal("http://localhost:5081", system.Value.BaseUrl);
    }

    [Fact]
    public void Development_settings_point_both_channels_at_the_sms_and_email_simulator_and_give_staff_a_contact()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.Development.json"))
            .Build();

        Assert.Equal("http://localhost:5082", Simulator(configuration, NotificationChannel.Sms).BaseUrl);
        Assert.Equal("http://localhost:5082", Simulator(configuration, NotificationChannel.Email).BaseUrl);
        var contacts = StaffContacts(configuration);
        var addresses = new List<string>();
        foreach (var role in CareLoop.Api.Infrastructure.Auth.DemoRoles.HumanRoles)
        {
            var contact = contacts.For(role);
            Assert.NotNull(contact);
            Assert.Equal(NotificationChannel.Email, contact.Channel);
            Assert.StartsWith("SYN-", contact.Address);
            addresses.Add(contact.Address!);
        }

        // One address per role, and no catch-all: a role nobody listed is not silently sent somewhere else.
        Assert.Equal(addresses.Count, addresses.Distinct().Count());
        Assert.Null(contacts.For("no-such-role"));
    }

    [Fact]
    public void Any_number_of_systems_bind_from_environment_style_keys()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ExternalSystems:Systems:SYN-HIS-A:BaseUrl"] = "http://his-a.test",
                ["ExternalSystems:Systems:SYN-EMR-B:BaseUrl"] = "http://emr-b.test",
                ["ExternalSystems:Systems:SYN-LAB-C:BaseUrl"] = "http://lab-c.test",
                ["Notifications:Sms:BaseUrl"] = "http://sms.test",
                ["Notifications:Email:InboundSecret"] = "SYN-SECRET",
                ["Notifications:StaffContacts:nurse-navigator:Channel"] = "Sms",
                ["Notifications:StaffContacts:nurse-navigator:Address"] = "SYN-TEL-NURSES",
                ["Notifications:StaffContacts:default:Channel"] = "Email",
                ["Notifications:StaffContacts:default:Address"] = "SYN-STAFF-TEAM",
                ["Notifications:StaffContacts:lab:Channel"] = "Email",
            })
            .Build();

        var options = Systems(configuration);

        Assert.Equal(3, options.Systems.Count);
        Assert.Equal("http://lab-c.test", options.Systems["syn-lab-c"].BaseUrl);
        Assert.Equal("http://sms.test", Simulator(configuration, NotificationChannel.Sms).BaseUrl);
        Assert.Equal("SYN-SECRET", Simulator(configuration, NotificationChannel.Email).InboundSecret);
        var contacts = StaffContacts(configuration);
        Assert.Equal("SYN-TEL-NURSES", contacts.For("Nurse-Navigator")!.Address);
        Assert.Equal(NotificationChannel.Sms, contacts.For("nurse-navigator")!.Channel);
        Assert.Equal("SYN-STAFF-TEAM", contacts.For("coordinator")!.Address);
        Assert.Equal("SYN-STAFF-TEAM", contacts.For("lab")!.Address);
    }

    [Fact]
    public void Base_settings_use_the_real_pathways_and_development_uses_demo_timing()
    {
        var baseOnly = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
            .Build();
        var development = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.Development.json"))
            .Build();

        Assert.Equal("Infrastructure/Pathways", baseOnly["Pathways:Directory"]);
        Assert.Equal("Infrastructure/Pathways/demo", development["Pathways:Directory"]);
    }

    private static ExternalSystemsOptions Systems(IConfiguration configuration)
    {
        var options = new ExternalSystemsOptions();
        configuration.GetSection(ExternalSystemsOptions.SectionName).Bind(options);
        return options;
    }

    private static SimulatorNotificationOptions Simulator(IConfiguration configuration, NotificationChannel channel)
    {
        var options = new SimulatorNotificationOptions();
        configuration.GetSection(SimulatorNotificationOptions.SectionFor(channel)).Bind(options);
        return options;
    }

    private static StaffContactOptions StaffContacts(IConfiguration configuration)
    {
        var options = new StaffContactOptions();
        configuration.GetSection(StaffContactOptions.SectionName).Bind(options.ByRole);
        return options;
    }
}

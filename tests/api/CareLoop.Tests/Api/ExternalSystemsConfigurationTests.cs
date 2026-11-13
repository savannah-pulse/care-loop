using CareLoop.Api.Infrastructure.Fhir;
using CareLoop.Api.Infrastructure.Notifications;
using Microsoft.Extensions.Configuration;

namespace CareLoop.Tests.Api;

/// <summary>
/// External systems bind as a named collection, and the simulator provider carries its own
/// addresses, in both the settings-file shape and the environment-variable shape Docker Compose uses.
/// </summary>
public sealed class ExternalSystemsConfigurationTests
{
    [Fact]
    public void Base_settings_configure_no_external_system_and_no_simulator_address()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
            .Build();

        Assert.Empty(Systems(configuration).Systems);
        Assert.True(string.IsNullOrEmpty(Simulator(configuration).PatientChannelBaseUrl));
        Assert.True(string.IsNullOrEmpty(Simulator(configuration).StaffInboxBaseUrl));
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
    public void Development_settings_point_the_simulator_provider_at_both_simulators()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.Development.json"))
            .Build();

        Assert.Equal("http://localhost:5082", Simulator(configuration).PatientChannelBaseUrl);
        Assert.Equal("http://localhost:5081", Simulator(configuration).StaffInboxBaseUrl);
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
                ["Notifications:Simulator:PatientChannelBaseUrl"] = "http://pch.test",
            })
            .Build();

        var options = Systems(configuration);

        Assert.Equal(3, options.Systems.Count);
        Assert.Equal("http://lab-c.test", options.Systems["syn-lab-c"].BaseUrl);
        Assert.Equal("http://pch.test", Simulator(configuration).PatientChannelBaseUrl);
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

    private static SimulatorNotificationOptions Simulator(IConfiguration configuration)
    {
        var options = new SimulatorNotificationOptions();
        configuration.GetSection(SimulatorNotificationOptions.SectionName).Bind(options);
        return options;
    }
}

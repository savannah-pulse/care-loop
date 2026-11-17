using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using PatientChannelSimulator.Inbound;
using PatientChannelSimulator.Outbound;
using PatientChannelSimulator.Scenarios;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Tests;

/// <summary>
/// Real (not skipped) checks that the scaffold's dependency-injection container builds and that
/// no stub throws from a constructor.
/// </summary>
public class ServiceRegistrationTests
{
    [Fact]
    public void Every_registered_service_resolves_without_throwing()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;

        Assert.NotNull(services.GetRequiredService<SimulatorStore>());
        Assert.NotNull(services.GetRequiredService<CareLoopCallbackClient>());
        Assert.NotNull(services.GetRequiredService<ReplySender>());
        Assert.NotNull(services.GetRequiredService<DeliverySimulator>());
        Assert.NotNull(services.GetRequiredService<AutomaticResponder>());
        Assert.NotNull(services.GetRequiredService<DeliveryReportRetrier>());
        Assert.NotNull(services.GetRequiredService<MessageReceiver>());
        Assert.NotNull(services.GetRequiredService<ScenarioLoader>());
        Assert.NotNull(services.GetRequiredService<ScenarioRunner>());
    }

    [Fact]
    public void System_clock_reports_utc_now()
    {
        using var provider = BuildProvider();
        var clock = provider.GetRequiredService<ISimulatorClock>();

        var before = DateTimeOffset.UtcNow;
        var now = clock.UtcNow;
        var after = DateTimeOffset.UtcNow;

        Assert.IsType<SystemSimulatorClock>(clock);
        Assert.Equal(TimeSpan.Zero, now.Offset);
        Assert.InRange(now, before, after);
    }

    private static ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CareLoop:FhirBaseUrl"] = "http://careloop.invalid/fhir",
                ["Scenarios:Directory"] = "scenarios",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddPatientChannelSimulator(configuration);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";

        public string ApplicationName { get; set; } = "PatientChannelSimulator.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

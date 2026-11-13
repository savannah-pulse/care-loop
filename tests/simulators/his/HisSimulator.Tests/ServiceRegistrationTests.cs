using HisSimulator.Inbound;
using HisSimulator.Outbound;
using HisSimulator.Scenarios;
using HisSimulator.State;
using HisSimulator.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HisSimulator.Tests;

/// <summary>
/// Checks that the dependency injection container builds and that no scaffolded service throws
/// from its constructor. These tests run now; they do not depend on stubs.
/// </summary>
public sealed class ServiceRegistrationTests
{
    public static TheoryData<Type> RegisteredServices => new()
    {
        typeof(ISimulatorClock),
        typeof(SimulatorStore),
        typeof(ScreeningResultBuilder),
        typeof(ClinicalReviewTaskBuilder),
        typeof(ConsultationBuilder),
        typeof(ReferralBuilder),
        typeof(AppointmentBuilder),
        typeof(OutcomeBuilder),
        typeof(ClosureBuilder),
        typeof(CareLoopFhirClient),
        typeof(OutboundEventSender),
        typeof(InboundFhirReceiver),
        typeof(ScenarioLoader),
        typeof(ScenarioRunner),
    };

    [Theory]
    [MemberData(nameof(RegisteredServices))]
    public void Every_registered_service_resolves(Type serviceType)
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService(serviceType));
    }

    [Fact]
    public void Options_bind_from_configuration()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["CareLoop:FhirBaseUrl"] = "http://careloop.test/fhir",
            ["Scenarios:Directory"] = "/app/scenarios",
        });

        Assert.Equal("http://careloop.test/fhir", provider.GetRequiredService<IOptions<CareLoopOptions>>().Value.FhirBaseUrl);
        Assert.Equal("/app/scenarios", provider.GetRequiredService<IOptions<ScenarioOptions>>().Value.Directory);
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?>? settings = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings ?? []).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddHisSimulator(configuration);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";

        public string ApplicationName { get; set; } = "HisSimulator.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

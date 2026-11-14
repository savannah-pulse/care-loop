using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PatientChannelSimulator.Inbound;
using PatientChannelSimulator.Outbound;
using PatientChannelSimulator.Scenarios;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator;

/// <summary>
/// Dependency-injection registration for the whole simulator.
/// </summary>
public static class ServiceRegistration
{
    /// <summary>
    /// Registers options, the in-memory store, the clock, the CareLoop FHIR client, the outbound
    /// builders, the inbound handlers and the scenario services.
    /// </summary>
    /// <remarks>
    /// Kept separate from <c>Program.cs</c> so a test can build the container and prove that every
    /// service resolves without starting a web server.
    /// </remarks>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">Application configuration supplying the <c>CareLoop</c>, <c>PatientChannel</c> and <c>Scenarios</c> sections.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddPatientChannelSimulator(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CareLoopOptions>(configuration.GetSection(CareLoopOptions.SectionName));
        services.Configure<ScenarioOptions>(configuration.GetSection(ScenarioOptions.SectionName));
        services.Configure<PatientChannelOptions>(configuration.GetSection(PatientChannelOptions.SectionName));

        // Enums travel as their names so the ClientApp and the log stay readable.
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        // State and time.
        services.AddSingleton<SimulatorStore>();
        services.AddSingleton<ISimulatorClock, SystemSimulatorClock>();

        // Outbound: patient channel -> CareLoop.
        services.AddHttpClient<CareLoopFhirClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<CareLoopOptions>>().Value;
            // A trailing slash makes relative paths such as "Task/{id}" resolve under /fhir/.
            client.BaseAddress = new Uri(options.FhirBaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        });
        services.AddSingleton<DeliveredCommunicationBuilder>();
        services.AddSingleton<DeliveryFailedCommunicationBuilder>();
        services.AddSingleton<AcknowledgementCommunicationBuilder>();
        services.AddSingleton<TeachBackCommunicationBuilder>();
        services.AddSingleton<CallMeCommunicationBuilder>();
        services.AddSingleton<ConfidenceRatingCommunicationBuilder>();
        services.AddTransient<PatientResponseSender>();
        services.AddTransient<CaseStatusReader>();

        // Inbound: CareLoop -> patient channel.
        services.AddSingleton<CommunicationRequestMapper>();
        services.AddTransient<DeliverySimulator>();
        services.AddTransient<AutomaticResponder>();
        services.AddTransient<CommunicationRequestReceiver>();
        services.AddHostedService<ResponseDispatcher>();

        // Scenarios.
        services.AddSingleton<ScenarioLoader>();
        services.AddTransient<ScenarioRunner>();

        return services;
    }
}

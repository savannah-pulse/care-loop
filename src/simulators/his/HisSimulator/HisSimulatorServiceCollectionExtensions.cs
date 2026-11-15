using System.Text.Json;
using System.Text.Json.Serialization;
using HisSimulator.Inbound;
using HisSimulator.Outbound;
using HisSimulator.Scenarios;
using HisSimulator.State;
using HisSimulator.Time;
using Microsoft.Extensions.Options;

namespace HisSimulator;

/// <summary>
/// Registers every HIS simulator service in the dependency injection container.
/// </summary>
/// <remarks>
/// Kept separate from <c>Program.cs</c> so tests can build the same container without
/// starting a web host and confirm that no stub throws from a constructor.
/// </remarks>
public static class HisSimulatorServiceCollectionExtensions
{
    /// <summary>
    /// Adds configuration binding, the in-memory store, the clock, the outbound FHIR builders
    /// and client, the inbound receiver and the scenario loader and runner.
    /// </summary>
    /// <param name="services">The service collection to add the registrations to.</param>
    /// <param name="configuration">
    /// Application configuration supplying the <c>CareLoop</c> and <c>Scenarios</c> sections.
    /// </param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    public static IServiceCollection AddHisSimulator(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<Middleware.SimulatorExceptionHandler>();
        services.Configure<CareLoopOptions>(configuration.GetSection(CareLoopOptions.SectionName));
        services.Configure<ScenarioOptions>(configuration.GetSection(ScenarioOptions.SectionName));

        // Enums travel as lower-case kebab strings (for example "screening-result"), matching the scenario schema.
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower)));

        // Time and state.
        services.AddSingleton<ISimulatorClock, SystemSimulatorClock>();
        services.AddSingleton<SimulatorStore>();

        // Outbound (HIS -> CareLoop): one builder per resource kind, the typed FHIR client and the sender.
        services.AddSingleton<ScreeningResultBuilder>();
        services.AddSingleton<ClinicalReviewTaskBuilder>();
        services.AddSingleton<ConsultationBuilder>();
        services.AddSingleton<ReferralBuilder>();
        services.AddSingleton<AppointmentBuilder>();
        services.AddSingleton<OutcomeBuilder>();
        services.AddSingleton<ClosureBuilder>();
        services.AddSingleton<ConsentBuilder>();
        services.AddHttpClient<CareLoopFhirClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<CareLoopOptions>>().Value;

            // A trailing slash makes relative request URIs such as "DiagnosticReport" resolve under /fhir/.
            client.BaseAddress = new Uri(options.FhirBaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        });
        services.AddTransient<OutboundEventSender>();

        // Inbound (CareLoop -> HIS).
        services.AddSingleton<InboundFhirReceiver>();

        // Scenarios.
        services.AddSingleton<ScenarioLoader>();
        services.AddSingleton<ScenarioRunner>();
        services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<ScenarioRunner>());

        return services;
    }
}

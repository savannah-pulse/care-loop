using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MessagingSimulator.Inbound;
using MessagingSimulator.Outbound;
using MessagingSimulator.Scenarios;
using MessagingSimulator.State;

namespace MessagingSimulator;

/// <summary>
/// Dependency-injection registration for the whole simulator.
/// </summary>
public static class ServiceRegistration
{
    /// <summary>
    /// Registers options, the in-memory store, the clock, the client that calls CareLoop back, the
    /// inbound handlers and the scenario services.
    /// </summary>
    /// <remarks>
    /// Kept separate from <c>Program.cs</c> so a test can build the container and prove that every
    /// service resolves without starting a web server.
    /// </remarks>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">Application configuration supplying the <c>CareLoop</c> and <c>Scenarios</c> sections.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddMessagingSimulator(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CareLoopOptions>(configuration.GetSection(CareLoopOptions.SectionName));
        services.Configure<ScenarioOptions>(configuration.GetSection(ScenarioOptions.SectionName));

        // Enums travel as their names so the ClientApp and the log stay readable.
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        // State and time.
        services.AddSingleton<SimulatorStore>();
        services.AddSingleton<MessageGate>();
        services.AddSingleton<ISimulatorClock, SystemSimulatorClock>();

        // Outbound: simulator -> CareLoop (delivery reports and replies).
        services.AddHttpClient<CareLoopCallbackClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<CareLoopOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
        });
        services.AddTransient<ReplySender>();

        // Inbound: CareLoop -> simulator (text messages and emails).
        services.AddTransient<DeliverySimulator>();
        services.AddTransient<AutomaticResponder>();
        services.AddTransient<DeliveryReportRetrier>();
        services.AddTransient<MessageReceiver>();
        services.AddHostedService<ResponseDispatcher>();

        // Scenarios.
        services.AddSingleton<ScenarioLoader>();
        services.AddTransient<ScenarioRunner>();

        return services;
    }
}

using HisSimulator.Inbound;
using HisSimulator.Outbound;
using HisSimulator.Scenarios;
using HisSimulator.State;
using HisSimulator.Time;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HisSimulator.Tests;

internal sealed class SimulatorHost : IAsyncDisposable
{
    private readonly WebApplication _app;
    internal CaptureHandler Handler { get; }
    internal SimulatorStore Store => _app.Services.GetRequiredService<SimulatorStore>();
    internal OutboundEventSender Sender => _app.Services.GetRequiredService<OutboundEventSender>();
    internal ScenarioRunner Runner => _app.Services.GetRequiredService<ScenarioRunner>();
    internal HttpClient Http { get; }

    private SimulatorHost(WebApplication app, CaptureHandler handler, HttpClient http) { _app = app; Handler = handler; Http = http; }

    internal static async Task<SimulatorHost> StartAsync(string? scenarios = null, CaptureHandler? handler = null, ISimulatorClock? clock = null)
    {
        handler ??= new CaptureHandler();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ScenarioRunner).Assembly.GetName().Name, EnvironmentName = "Development",
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["Scenarios:Directory"] = scenarios ?? TestPaths.ScenariosDirectory;
        builder.Services.AddHisSimulator(builder.Configuration);
        builder.Services.AddSingleton<ISimulatorClock>(clock ?? new FixedClock());
        builder.Services.AddHttpClient<CareLoopFhirClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.MapFhirInboundEndpoints(); app.MapStaffInboxEndpoints(); app.MapFhirMessageLogEndpoints();
        app.MapClinicianActionEndpoints(); app.MapScenarioEndpoints();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new SimulatorHost(app, handler, new HttpClient { BaseAddress = new Uri(address) });
    }

    public async ValueTask DisposeAsync()
    {
        Http.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}

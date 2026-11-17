using PatientChannelSimulator;
using PatientChannelSimulator.Inbound;
using PatientChannelSimulator.Scenarios;

// SMS and email simulator (WS8; the project keeps its patient-channel name). SYNTHETIC DATA ONLY.
// Stands in for an SMS and email service: CareLoop posts text messages and emails as plain JSON,
// and the simulator calls back with delivery reports and replies. No FHIR crosses it.

var builder = WebApplication.CreateBuilder(args);

// Fail at start-up, not at first request, if any registration cannot be resolved.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

builder.Services.AddPatientChannelSimulator(builder.Configuration);
builder.Services.AddOpenApi();

var app = builder.Build();

// The host's own route prefixes are never client-side routes: an unknown path under one of them is
// a wrong address and answers 404, not the ClientApp shell with 200.
app.Use(async (context, next) =>
{
    if (context.GetEndpoint()?.Metadata.GetMetadata<ClientAppFallback>() is not null
        && ClientAppFallback.IsHostPath(context.Request.Path))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next(context);
});

app.MapOpenApi();

// Serves the built ClientApp from wwwroot when it exists; harmless when it does not.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => TypedResults.Ok(new HealthResponse("ok", "patient-channel-simulator")))
    .WithName("GetHealth")
    .WithSummary("Liveness probe for the SMS and email simulator (SIMULATED service).")
    .WithTags("Health");

app.MapMessageEndpoints();
app.MapInboxEndpoints();
app.MapPatientBehaviourEndpoints();
app.MapMessageLogEndpoints();
app.MapScenarioEndpoints();

// Single-page-app fallback: unknown GET paths return the ClientApp shell so client-side routes
// survive a refresh. Returns 404 when the ClientApp has not been built into wwwroot.
app.MapFallbackToFile("index.html").WithMetadata(ClientAppFallback.Instance);

app.Run();

/// <summary>
/// The simulator's entry point. Declared public so the test project can host the real
/// application in memory.
/// </summary>
public partial class Program;

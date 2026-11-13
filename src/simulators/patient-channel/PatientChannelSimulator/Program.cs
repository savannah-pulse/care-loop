using PatientChannelSimulator;
using PatientChannelSimulator.Inbound;
using PatientChannelSimulator.Scenarios;

// Patient-channel simulator (WS8). SYNTHETIC DATA ONLY.
// Stands in for the patient-facing channel and talks to CareLoop only through FHIR R4.

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

app.MapOpenApi();

// Serves the built ClientApp from wwwroot when it exists; harmless when it does not.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => TypedResults.Ok(new HealthResponse("ok", "patient-channel-simulator")))
    .WithName("GetHealth")
    .WithSummary("Liveness probe for the patient-channel simulator (SIMULATED service).")
    .WithTags("Health");

app.MapFhirInboundEndpoints();
app.MapInboxEndpoints();
app.MapPatientBehaviourEndpoints();
app.MapMessageLogEndpoints();
app.MapCaseStatusEndpoints();
app.MapScenarioEndpoints();

// Single-page-app fallback: unknown GET paths return the ClientApp shell so client-side routes
// survive a refresh. Returns 404 when the ClientApp has not been built into wwwroot.
app.MapFallbackToFile("index.html");

app.Run();

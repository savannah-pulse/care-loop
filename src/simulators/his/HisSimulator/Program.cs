using HisSimulator;
using HisSimulator.Inbound;
using HisSimulator.Outbound;
using HisSimulator.Scenarios;

// HIS simulator (WS7): a SYNTHETIC stand-in for a hospital information system.
// It exchanges FHIR R4 with CareLoop and shares no code with it.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHisSimulator(builder.Configuration);
builder.Services.AddOpenApi();

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapOpenApi();

// Serves the built ClientApp from wwwroot. Both calls are harmless when wwwroot is absent
// (local development before `yarn build`, or the test host).
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHealthEndpoints();
app.MapFhirInboundEndpoints();
app.MapStaffInboxEndpoints();
app.MapFhirMessageLogEndpoints();
app.MapClinicianActionEndpoints();
app.MapScenarioEndpoints();

// SPA fallback: any unmatched GET returns the ClientApp shell (404 when wwwroot is absent).
app.MapFallbackToFile("index.html");

app.Run();

using System.Text.Json.Serialization;
using CareLoop.Api.Features.Ai;
using CareLoop.Api.Features.Cases;
using CareLoop.Api.Features.Common;
using CareLoop.Api.Features.Exceptions;
using CareLoop.Api.Features.Fhir;
using CareLoop.Api.Features.Metrics;
using CareLoop.Api.Features.Notifications;
using CareLoop.Api.Features.Programmes;
using CareLoop.Api.Infrastructure;
using CareLoop.Api.Infrastructure.Ai;
using CareLoop.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Fail at startup, not at first request, if a service cannot be constructed.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// The OpenAPI document includes the XML documentation comments of endpoints and contracts.
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<OpenApiLineEndingTransformer>());

builder.Services
    .AddCoreAndPersistence(builder.Configuration)
    .AddIntake()
    .AddFhirBoundary(builder.Configuration)
    .AddOutboxAndNotifications(builder.Configuration)
    .AddDeadlines(builder.Configuration)
    .AddAi(builder.Configuration);

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<CareLoopDbContext>().Database.MigrateAsync();
}

// Read the prompt files now, so one that cannot be used stops the host instead of failing a request.
app.Services.GetRequiredService<AiPrompts>();

app.MapOpenApi();

app.MapHealth();
app.MapFhirEndpoints();
app.MapReceiveInboundNotification();
app.MapListCases();
app.MapGetCase();
app.MapGetTimeline();
app.MapGetExceptions();
app.MapGetMetrics();
app.MapGetProgrammes();
app.MapAcknowledgeEscalation();
app.MapRecordCallOutcome();
app.MapRunSimulation();
app.MapRoleBriefing();
app.MapImpactSummary();
app.MapPatientMessageDraft();

await app.RunAsync();

/// <summary>
/// The API entry point. Declared partial so tests can reference the assembly's startup type.
/// </summary>
public partial class Program;

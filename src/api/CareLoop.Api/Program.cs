using System.Text.Json;
using System.Text.Json.Serialization;
using CareLoop.Api;
using CareLoop.Api.Endpoints;
using CareLoop.Application;
using CareLoop.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<BadRequestExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddCareLoopApplication();
builder.Services.AddCareLoopPersistence();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (app.Configuration.GetValue<bool>("CareLoop:Database:EnsureCreated"))
{
    await app.Services.InitializeCareLoopDatabaseAsync();
}

app.MapEndpointModules();

app.Run();

public partial class Program;

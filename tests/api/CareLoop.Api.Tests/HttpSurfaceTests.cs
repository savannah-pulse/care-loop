using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace CareLoop.Api.Tests;

public sealed class HttpSurfaceTests(CareLoopApiFactory factory) : IClassFixture<CareLoopApiFactory>
{
    [Fact]
    public async Task Malformed_json_is_a_client_error_not_a_server_error()
    {
        using var client = factory.CreateClient();
        using var content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/events", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Missing_envelope_fields_are_reported_as_validation_errors()
    {
        using var client = factory.CreateClient();

        var (response, body) = await TestEvents.PostAsync(client, new { eventType = "ResultReceived" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = body.GetProperty("errors");
        Assert.True(errors.TryGetProperty("sourceSystem", out _));
        Assert.True(errors.TryGetProperty("sourceEventId", out _));
        Assert.True(errors.TryGetProperty("occurredAt", out _));
    }

    [Fact]
    public async Task Development_serves_an_openapi_document_for_every_endpoint()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"careloop-openapi-{Guid.NewGuid():N}.db");
        try
        {
            using var development = factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:CareLoop"] = $"Data Source={databasePath};Pooling=False",
                }));
            });
            using var client = development.CreateClient();

            var response = await client.GetAsync("/openapi/v1.json");
            var document = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("/api/events", document);
            Assert.Contains("/api/cases/{id}/timeline", document);
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }
}

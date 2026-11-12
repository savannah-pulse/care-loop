using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CareLoop.Api.Tests;

/// <summary>
/// Hosts the API against an isolated SQLite file. Uses the "Testing" environment so developer
/// settings (such as the placeholder owner queue) never leak into tests.
/// </summary>
public class CareLoopApiFactory : WebApplicationFactory<Program>
{
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"careloop-test-{Guid.NewGuid():N}.db");
    private readonly string? defaultQueueId;

    public CareLoopApiFactory() : this("synthetic-test-queue")
    {
    }

    protected CareLoopApiFactory(string? defaultQueueId) => this.defaultQueueId = defaultQueueId;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:CareLoop", $"Data Source={databasePath};Pooling=False");
        builder.UseSetting("CareLoop:Database:EnsureCreated", "true");
        if (defaultQueueId is not null)
        {
            builder.UseSetting("CareLoop:OwnerAssignment:DefaultQueueId", defaultQueueId);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }
    }
}

using CareLoop.Application.Abstractions;
using CareLoop.Application.Queries;
using CareLoop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CareLoop.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "CareLoop";

    /// <summary>
    /// Registers the coordination store. SQLite is a local-development placeholder: the relational database is a
    /// pending team decision, and the provider call below is the only line that changes when it is made.
    /// </summary>
    public static IServiceCollection AddCareLoopPersistence(this IServiceCollection services)
    {
        services.AddDbContext<CareLoopDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"Connection string '{ConnectionStringName}' is not configured. Set ConnectionStrings:{ConnectionStringName}.");
            options.UseSqlite(connectionString);
        });

        services.AddScoped<ICaseRepository, CaseRepository>();
        services.AddScoped<ICaseEventStore, CaseEventStore>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICaseQueries, CaseQueries>();
        return services;
    }

    /// <summary>
    /// Creates the schema for local development and tests. Real environments should use migrations once the
    /// database provider is chosen.
    /// </summary>
    public static async Task InitializeCareLoopDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLoopDbContext>();
        await db.Database.EnsureCreatedAsync(cancellationToken);
    }
}

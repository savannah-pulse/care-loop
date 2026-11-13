using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CareLoop.Api.Infrastructure.Persistence;

/// <summary>
/// Creates the database context for the EF Core command-line tools (<c>dotnet ef migrations add</c>).
/// </summary>
/// <remarks>
/// Used at design time only. It never connects to a database when adding a migration, so the
/// connection string is a local placeholder and contains no secret. Override it with the
/// <c>CARELOOP_DESIGN_CONNECTION</c> environment variable when running <c>dotnet ef database update</c>.
/// </remarks>
public sealed class CareLoopDbContextFactory : IDesignTimeDbContextFactory<CareLoopDbContext>
{
    /// <inheritdoc/>
    public CareLoopDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("CARELOOP_DESIGN_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=careloop;Username=careloop;Password=design-time-placeholder";

        var options = new DbContextOptionsBuilder<CareLoopDbContext>()
            .UseNpgsql(connection)
            .Options;

        return new CareLoopDbContext(options);
    }
}

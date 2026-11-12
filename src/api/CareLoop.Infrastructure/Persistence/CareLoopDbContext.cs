using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CareLoop.Infrastructure.Persistence;

/// <summary>
/// EF Core model for the coordination store. Entity mappings live in <c>Configurations/</c>, one configuration
/// class per entity, and are discovered automatically so engineers do not edit this class to add a table.
/// </summary>
public sealed class CareLoopDbContext(DbContextOptions<CareLoopDbContext> options) : DbContext(options)
{
    public DbSet<CoordinationCase> Cases => Set<CoordinationCase>();

    public DbSet<CaseEvent> Events => Set<CaseEvent>();

    public DbSet<StageTransition> StageTransitions => Set<StageTransition>();

    public DbSet<CaseException> CaseExceptions => Set<CaseException>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CareLoopDbContext).Assembly);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite cannot order or compare DateTimeOffset natively. Remove when the production database is chosen.
        if (Database.IsSqlite())
        {
            configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
        }
    }
}

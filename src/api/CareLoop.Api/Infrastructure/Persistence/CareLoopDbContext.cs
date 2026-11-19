using CareLoop.Api.Infrastructure.Persistence.Audit;
using CareLoop.Api.Infrastructure.Persistence.Outbox;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace CareLoop.Api.Infrastructure.Persistence;

/// <summary>
/// The CareLoop database: case projections, the append-only source and audit events, the read audit, the outbox and handoff summaries.
/// </summary>
/// <remarks>
/// Writes to case state happen only in <see cref="EfCaseRepository"/>. Read slices query this
/// context directly with no-tracking projections, and append a <see cref="CaseReadRecord"/> for each read. The mapping is provider-neutral: nothing here
/// may depend on PostgreSQL-specific features, so Azure SQL remains an option.
/// </remarks>
public sealed class CareLoopDbContext : DbContext
{
    /// <summary>
    /// Creates the context.
    /// </summary>
    /// <param name="options">Provider and connection settings.</param>
    public CareLoopDbContext(DbContextOptions<CareLoopDbContext> options)
        : base(options)
    {
    }

    /// <summary>The current projection of every case.</summary>
    public DbSet<Case> Cases => Set<Case>();

    /// <summary>Every source event ever stored. Append-only.</summary>
    public DbSet<SourceEvent> SourceEvents => Set<SourceEvent>();

    /// <summary>The audit trail. Append-only.</summary>
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    /// <summary>Domain events waiting to be delivered to subscribers.</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>Per-subscriber delivery state of each outbox message.</summary>
    public DbSet<OutboxDelivery> OutboxDeliveries => Set<OutboxDelivery>();

    /// <summary>Every staff read of case data (C4, C19). Append-only; the only table read slices write to.</summary>
    public DbSet<CaseReadRecord> CaseReads => Set<CaseReadRecord>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CareLoopDbContext).Assembly);
    }
}

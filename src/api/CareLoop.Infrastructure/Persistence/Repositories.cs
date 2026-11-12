using CareLoop.Application.Abstractions;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CareLoop.Infrastructure.Persistence;

internal sealed class CaseRepository(CareLoopDbContext db) : ICaseRepository
{
    public Task<CoordinationCase?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Cases.Include(c => c.Exceptions).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<CoordinationCase?> FindByResultAsync(string sourceSystem, string resultRef, CancellationToken cancellationToken) =>
        db.Cases.Include(c => c.Exceptions)
            .FirstOrDefaultAsync(c => c.SourceSystem == sourceSystem && c.ResultRef == resultRef, cancellationToken);

    public void Add(CoordinationCase @case) => db.Cases.Add(@case);
}

internal sealed class CaseEventStore(CareLoopDbContext db) : ICaseEventStore
{
    public Task<CaseEvent?> FindBySourceKeyAsync(string sourceSystem, string sourceEventId, CancellationToken cancellationToken) =>
        db.Events.AsNoTracking()
            .FirstOrDefaultAsync(e => e.SourceSystem == sourceSystem && e.SourceEventId == sourceEventId, cancellationToken);

    public void Append(CaseEvent @event) => db.Events.Add(@event);
}

/// <summary>Translates provider errors into the application's exceptions; the only place that knows the provider's codes.</summary>
internal sealed class UnitOfWork(CareLoopDbContext db) : IUnitOfWork
{
    private const int SqliteConstraintPrimaryKey = 1555;
    private const int SqliteConstraintUnique = 2067;

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException
        {
            SqliteExtendedErrorCode: SqliteConstraintUnique or SqliteConstraintPrimaryKey
        })
        {
            throw new DuplicateKeyException(ex);
        }
    }
}

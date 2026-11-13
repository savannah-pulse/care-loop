using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Time;

namespace CareLoop.Api.Infrastructure.Persistence;

/// <summary>
/// The EF Core implementation of the single write path for cases.
/// </summary>
/// <remarks>
/// Will, inside one database transaction in <see cref="SaveAsync"/>: insert the source event
/// (the unique index on source system plus source event identifier rejects a duplicate); insert
/// or update the case projection with an optimistic concurrency check on its version; append an
/// <see cref="AuditEvent"/> built from the projection as loaded and as saved; and write one
/// outbox message, with one delivery row per registered subscriber, for each domain event on the
/// case. If any part fails nothing is stored. No other class writes case state.
/// </remarks>
public sealed class EfCaseRepository : ICaseRepository
{
    private readonly CareLoopDbContext _db;
    private readonly IClock _clock;

    /// <summary>
    /// Creates the repository.
    /// </summary>
    /// <param name="db">The database context for this unit of work.</param>
    /// <param name="clock">Supplies the outbox creation time.</param>
    public EfCaseRepository(CareLoopDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <inheritdoc/>
    public Task<Case?> GetAsync(Guid caseId, CancellationToken ct)
    {
        // TODO(WS-2): load the tracked case with its owned tracks and remember its prior projection for the audit record.
        throw new NotImplementedException("WS2: EfCaseRepository.GetAsync");
    }

    /// <inheritdoc/>
    public Task<Case?> FindBySourceAsync(string sourceSystem, string sourceResultId, CancellationToken ct)
    {
        // TODO(WS-2): look up by the unique (SourceSystem, SourceResultId) index.
        throw new NotImplementedException("WS2: EfCaseRepository.FindBySourceAsync");
    }

    /// <inheritdoc/>
    public Task<bool> EventExistsAsync(string sourceSystem, string sourceEventId, CancellationToken ct)
    {
        // TODO(WS-2): AnyAsync on SourceEvents by (SourceSystem, SourceEventId).
        throw new NotImplementedException("WS2: EfCaseRepository.EventExistsAsync");
    }

    /// <inheritdoc/>
    public Task SaveAsync(Case @case, SourceEvent appliedEvent, CancellationToken ct)
    {
        // TODO(WS-2): one transaction: source event, case projection, audit event, outbox messages and deliveries.
        throw new NotImplementedException("WS2: EfCaseRepository.SaveAsync");
    }
}

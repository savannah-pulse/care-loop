using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;

namespace CareLoop.Application.Abstractions;

public interface ICaseRepository
{
    Task<CoordinationCase?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<CoordinationCase?> FindByResultAsync(string sourceSystem, string resultRef, CancellationToken cancellationToken);

    void Add(CoordinationCase @case);
}

public interface ICaseEventStore
{
    Task<CaseEvent?> FindBySourceKeyAsync(string sourceSystem, string sourceEventId, CancellationToken cancellationToken);

    void Append(CaseEvent @event);
}

/// <summary>Commits everything staged by the repositories atomically.</summary>
public interface IUnitOfWork
{
    /// <exception cref="DuplicateKeyException">A uniqueness rule (idempotency key, one case per result) was violated.</exception>
    /// <exception cref="ConcurrencyConflictException">The case changed since it was read.</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class DuplicateKeyException(Exception? inner = null)
    : Exception("A uniqueness constraint was violated.", inner);

public sealed class ConcurrencyConflictException(Exception? inner = null)
    : Exception("The case was modified concurrently.", inner);

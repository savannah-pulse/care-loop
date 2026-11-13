using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Api.Infrastructure.Persistence.Outbox;
using CareLoop.Domain.Cases;
using CareLoop.Tests.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CareLoop.Tests.Api.Fhir;

/// <summary>
/// An in-memory SQLite coordination store for the FHIR read and publish paths, which query the database directly.
/// </summary>
internal sealed class FhirStore : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private int _events;

    /// <summary>Opens the store and creates the schema.</summary>
    public FhirStore()
    {
        _connection.Open();
        using var db = NewContext();
        db.Database.EnsureCreated();
    }

    /// <summary>The clock the stored cases use.</summary>
    public FakeClock Clock { get; } = new();

    /// <summary>A fresh context on the store.</summary>
    public CareLoopDbContext NewContext() =>
        new(new DbContextOptionsBuilder<CareLoopDbContext>().UseSqlite(_connection).Options);

    /// <summary>Saves a case through the one write path.</summary>
    public async System.Threading.Tasks.Task SaveAsync(Case @case)
    {
        await using var db = NewContext();
        var repository = new EfCaseRepository(db, Clock, new OutboxSubscriberRegistry(() => ["FhirPublisher"]));
        await repository.SaveAsync(@case, TestData.AppliedEvent(@case, $"SYN-EVT-STORE-{++_events}", Clock.UtcNow), CancellationToken.None);
    }

    /// <inheritdoc/>
    public void Dispose() => _connection.Dispose();
}

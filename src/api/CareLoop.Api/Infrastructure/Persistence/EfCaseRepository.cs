using System.Text.Json;
using System.Text.Json.Serialization;
using CareLoop.Api.Infrastructure.Persistence.Outbox;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Events.DomainEvents;
using CareLoop.Domain.Time;
using Microsoft.EntityFrameworkCore;

namespace CareLoop.Api.Infrastructure.Persistence;

/// <summary>
/// The EF Core implementation of the single write path for cases.
/// </summary>
/// <remarks>
/// <para>
/// Inside one database transaction, <see cref="SaveAsync"/>: inserts the source event (the unique
/// index on source system plus source event identifier rejects a duplicate); inserts or updates the
/// case projection with an optimistic concurrency check on its version; appends an
/// <see cref="AuditEvent"/> built from the projection as loaded and as saved; and writes one outbox
/// message, with one delivery row per registered subscriber, for each domain event on the case. If
/// any part fails nothing is stored. No other class writes case state.
/// </para>
/// <para>
/// The repository is scoped to one unit of work: a case loaded through it is tracked, and its
/// projection at load time is remembered so the audit record can show what changed.
/// </para>
/// </remarks>
public sealed class EfCaseRepository : ICaseRepository
{
    /// <summary>
    /// Serializer settings for outbox payloads: camelCase, enums as names, so the dispatcher and a
    /// person reading the table see the same thing.
    /// </summary>
    private static readonly JsonSerializerOptions OutboxSerializerOptions = CreateOutboxSerializerOptions();

    private readonly CareLoopDbContext _db;
    private readonly IClock _clock;
    private readonly OutboxSubscriberRegistry _subscribers;
    private readonly Dictionary<Guid, string> _priorProjections = [];

    /// <summary>
    /// Creates the repository.
    /// </summary>
    /// <param name="db">The database context for this unit of work.</param>
    /// <param name="clock">Supplies the outbox creation time.</param>
    /// <param name="subscribers">Names the subscribers each outbox message gets a delivery row for.</param>
    public EfCaseRepository(CareLoopDbContext db, IClock clock, OutboxSubscriberRegistry subscribers)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(subscribers);

        _db = db;
        _clock = clock;
        _subscribers = subscribers;
    }

    /// <inheritdoc/>
    public async Task<Case?> GetAsync(Guid caseId, CancellationToken ct)
    {
        var @case = await _db.Cases.FirstOrDefaultAsync(c => c.Id == caseId, ct);
        Remember(@case);
        return @case;
    }

    /// <inheritdoc/>
    public async Task<Case?> FindBySourceAsync(string sourceSystem, string sourceResultId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceResultId);

        var @case = await _db.Cases.FirstOrDefaultAsync(
            c => c.SourceSystem == sourceSystem && c.SourceResultId == sourceResultId, ct);
        Remember(@case);
        return @case;
    }

    /// <inheritdoc/>
    public Task<bool> EventExistsAsync(string sourceSystem, string sourceEventId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEventId);

        return _db.SourceEvents.AnyAsync(e => e.SourceSystem == sourceSystem && e.SourceEventId == sourceEventId, ct);
    }

    /// <inheritdoc/>
    /// <exception cref="DbUpdateConcurrencyException">The case was changed by someone else since it was loaded.</exception>
    public async Task SaveAsync(Case @case, SourceEvent appliedEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(@case);
        ArgumentNullException.ThrowIfNull(appliedEvent);

        if (appliedEvent.CaseId != @case.Id)
        {
            throw new InvalidOperationException(
                $"Source event '{appliedEvent.SourceSystem}/{appliedEvent.SourceEventId}' belongs to case {appliedEvent.CaseId}, not to case {@case.Id}.");
        }

        var ownsTransaction = _db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;

        try
        {
            var entry = _db.Entry(@case);
            var isNew = entry.State == EntityState.Detached;
            var priorProjection = isNew ? null : _priorProjections.GetValueOrDefault(@case.Id);

            if (isNew)
            {
                _db.Cases.Add(@case);
            }

            var versionProperty = entry.Property(c => c.Version);
            versionProperty.CurrentValue = @case.Version + 1;

            _db.SourceEvents.Add(appliedEvent);

            var now = _clock.UtcNow;
            _db.AuditEvents.Add(BuildAuditEvent(@case, appliedEvent, priorProjection));

            foreach (var domainEvent in @case.DomainEvents)
            {
                _db.OutboxMessages.Add(BuildOutboxMessage(domainEvent, now));
            }

            await _db.SaveChangesAsync(ct);

            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }
        }
        catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException && ex.Entries.Any(e => e.Entity is SourceEvent))
        {
            await RollbackAsync(transaction, ct);
            Detach(appliedEvent, @case);
            throw new InvalidOperationException(
                $"Source event '{appliedEvent.SourceSystem}/{appliedEvent.SourceEventId}' is already stored.", ex);
        }
        catch
        {
            await RollbackAsync(transaction, ct);
            Detach(appliedEvent, @case);
            throw;
        }

        // After the commit, so a failure here can never trigger a rollback of what is already stored
        // (which would fail in turn and hide the original error).
        @case.ClearDomainEvents();
        _priorProjections[@case.Id] = CaseProjection.ToJson(@case);
    }

    private void Remember(Case? @case)
    {
        if (@case is not null)
        {
            _priorProjections[@case.Id] = CaseProjection.ToJson(@case);
        }
    }

    private static async Task RollbackAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction, CancellationToken ct)
    {
        if (transaction is not null)
        {
            await transaction.RollbackAsync(ct);
        }
    }

    /// <summary>
    /// Undoes the pending additions after a failed save so the unit of work can continue, and
    /// resets the case's version so a retry presents the value that was loaded.
    /// </summary>
    private void Detach(SourceEvent appliedEvent, Case @case)
    {
        foreach (var entry in _db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList())
        {
            if (entry.Entity is Case tracked && tracked.Id == @case.Id)
            {
                continue;
            }

            entry.State = EntityState.Detached;
        }

        var caseEntry = _db.Entry(@case);
        if (caseEntry.State == EntityState.Added)
        {
            caseEntry.State = EntityState.Detached;
            caseEntry.Property(c => c.Version).CurrentValue = 0;
        }
        else if (caseEntry.State != EntityState.Detached)
        {
            caseEntry.Property(c => c.Version).CurrentValue = caseEntry.Property(c => c.Version).OriginalValue;
        }

        _ = appliedEvent;
    }

    private static AuditEvent BuildAuditEvent(Case @case, SourceEvent appliedEvent, string? priorProjection)
    {
        var actorReference = appliedEvent.Actor?.Reference ?? appliedEvent.SourceSystem;
        var actorRole = appliedEvent.Actor?.Role ?? "system";
        var reason = appliedEvent.DispositionReason
            ?? (appliedEvent.Disposition == SourceEventDisposition.Applied
                ? $"{appliedEvent.Type} applied"
                : $"{appliedEvent.Type} rejected");

        return new AuditEvent
        {
            Id = Guid.NewGuid(),
            CaseId = @case.Id,
            SourceEventId = appliedEvent.Id,
            EventType = appliedEvent.Type,
            Disposition = appliedEvent.Disposition,
            ActorReference = actorReference,
            ActorRole = actorRole,
            SourceTimestamp = appliedEvent.OccurredAt,
            ReceivedTimestamp = appliedEvent.ReceivedAt,
            PriorProjectionJson = priorProjection,
            NewProjectionJson = CaseProjection.ToJson(@case),
            Reason = reason.Length > 1000 ? reason[..1000] : reason,
            EvidenceReference = appliedEvent.EvidenceReference,
        };
    }

    private OutboxMessage BuildOutboxMessage(IDomainEvent domainEvent, DateTimeOffset now)
    {
        var message = new OutboxMessage
        {
            Id = domainEvent.EventId,
            CaseId = domainEvent.CaseId,
            EventType = domainEvent.GetType().Name,
            PayloadJson = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), OutboxSerializerOptions),
            OccurredAt = domainEvent.OccurredAt,
            CreatedAt = now,
        };

        foreach (var subscriber in _subscribers.SubscriberNames)
        {
            message.Deliveries.Add(new OutboxDelivery
            {
                Id = Guid.NewGuid(),
                OutboxMessageId = message.Id,
                Subscriber = subscriber,
                Status = OutboxDeliveryStatus.Pending,
                Attempts = 0,
                NextAttemptAt = null,
            });
        }

        return message;
    }

    private static JsonSerializerOptions CreateOutboxSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

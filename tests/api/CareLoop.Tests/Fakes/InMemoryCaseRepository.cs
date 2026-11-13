using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Events.DomainEvents;

namespace CareLoop.Tests.Fakes;

/// <summary>
/// An in-memory <see cref="ICaseRepository"/> for tests and the simulation replay.
/// </summary>
/// <remarks>
/// Mirrors the guarantees of the real repository that tests rely on: a duplicate source event is
/// refused, the domain events raised on the case are captured (as the outbox would) and cleared,
/// and an <see cref="AuditEvent"/> is appended with the case projection as loaded and as saved,
/// in the same <see cref="CaseProjection"/> shape the real repository writes.
/// </remarks>
public sealed class InMemoryCaseRepository : ICaseRepository
{
    private readonly Dictionary<Guid, Case> _cases = [];
    private readonly Dictionary<Guid, string> _projections = [];
    private readonly List<SourceEvent> _events = [];
    private readonly List<AuditEvent> _audit = [];
    private readonly List<IDomainEvent> _published = [];

    /// <summary>Every stored case.</summary>
    public IReadOnlyCollection<Case> Cases => _cases.Values;

    /// <summary>Every stored source event, in the order saved.</summary>
    public IReadOnlyList<SourceEvent> Events => _events;

    /// <summary>Every audit entry, in the order saved.</summary>
    public IReadOnlyList<AuditEvent> AuditEvents => _audit;

    /// <summary>Every domain event that would have been written to the outbox, in the order saved.</summary>
    public IReadOnlyList<IDomainEvent> PublishedDomainEvents => _published;

    /// <inheritdoc/>
    public Task<Case?> GetAsync(Guid caseId, CancellationToken ct) =>
        Task.FromResult(_cases.GetValueOrDefault(caseId));

    /// <inheritdoc/>
    public Task<Case?> FindBySourceAsync(string sourceSystem, string sourceResultId, CancellationToken ct) =>
        Task.FromResult(_cases.Values.FirstOrDefault(c =>
            c.SourceSystem == sourceSystem && c.SourceResultId == sourceResultId));

    /// <inheritdoc/>
    public Task<bool> EventExistsAsync(string sourceSystem, string sourceEventId, CancellationToken ct) =>
        Task.FromResult(_events.Any(e => e.SourceSystem == sourceSystem && e.SourceEventId == sourceEventId));

    /// <inheritdoc/>
    public Task SaveAsync(Case @case, SourceEvent appliedEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(@case);
        ArgumentNullException.ThrowIfNull(appliedEvent);

        if (_events.Any(e => e.SourceSystem == appliedEvent.SourceSystem && e.SourceEventId == appliedEvent.SourceEventId))
        {
            throw new InvalidOperationException(
                $"Source event '{appliedEvent.SourceSystem}/{appliedEvent.SourceEventId}' is already stored.");
        }

        var prior = _projections.GetValueOrDefault(@case.Id);
        var current = CaseProjection.ToJson(@case);

        _events.Add(appliedEvent);
        _cases[@case.Id] = @case;
        _projections[@case.Id] = current;
        _audit.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            CaseId = @case.Id,
            SourceEventId = appliedEvent.Id,
            EventType = appliedEvent.Type,
            Disposition = appliedEvent.Disposition,
            ActorReference = appliedEvent.Actor?.Reference ?? appliedEvent.SourceSystem,
            ActorRole = appliedEvent.Actor?.Role ?? "system",
            SourceTimestamp = appliedEvent.OccurredAt,
            ReceivedTimestamp = appliedEvent.ReceivedAt,
            PriorProjectionJson = prior,
            NewProjectionJson = current,
            Reason = appliedEvent.DispositionReason ?? $"{appliedEvent.Type} {appliedEvent.Disposition}",
            EvidenceReference = appliedEvent.EvidenceReference,
        });
        _published.AddRange(@case.DomainEvents);
        @case.ClearDomainEvents();

        return Task.CompletedTask;
    }
}

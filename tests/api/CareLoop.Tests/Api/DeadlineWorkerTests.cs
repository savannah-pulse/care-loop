using System.Text.Json;
using System.Text.Json.Serialization;
using CareLoop.Api.Infrastructure;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Api.Infrastructure.Persistence.Outbox;
using CareLoop.Api.Worker;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Events.DomainEvents;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Rules;
using CareLoop.Domain.Time;
using CareLoop.Domain.Tracks;
using CareLoop.Tests.Domain;
using CareLoop.Tests.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CareLoop.Tests.Api;

/// <summary>The worker persists warnings, exceptions, escalation and audit through the real SQLite write path.</summary>
public sealed class DeadlineWorkerTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly FakeClock _clock = new();
    private readonly ServiceProvider _services;
    private readonly DeadlineWorker _worker;
    private readonly TestLogger _logger = new();
    private Func<Task>? _beforeDeadlineSave;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public DeadlineWorkerTests()
    {
        _connection.Open();
        var services = new ServiceCollection();
        services.AddDbContext<CareLoopDbContext>(o => o.UseSqlite(_connection));
        services.AddSingleton<IClock>(_clock);
        services.AddSingleton<IPathwayProvider>(DomainTestData.Provider());
        services.AddSingleton(new OutboxSubscriberRegistry(() => ["FhirPublisher", "NotificationRouter"]));
        services.AddScoped<EfCaseRepository>();
        services.AddScoped<ICaseRepository>(sp => new InterceptingRepository(sp.GetRequiredService<EfCaseRepository>(), this));
        _services = services.BuildServiceProvider();
        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<CareLoopDbContext>().Database.EnsureCreated();
        _worker = NewWorker();
    }

    public void Dispose()
    {
        _worker.Dispose();
        _services.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Approaching_deadline_raises_DeadlineApproaching_once()
    {
        var @case = await SeedAsync();
        SetWarningTime(@case);
        Assert.Equal(1, await SweepAsync());
        Assert.Equal(0, await SweepAsync());
        using var restarted = NewWorker();
        Assert.Equal(0, await restarted.SweepAsync(CancellationToken.None));
        var warning = Assert.Single(await RaisedAsync<DeadlineApproaching>());
        Assert.Equal(@case.Owner, warning.CurrentOwner);
        Assert.Equal(@case.NextAction, warning.NextAction);
        Assert.Equal(@case.DueAt, warning.DueAt);
        Assert.Equal(_clock.UtcNow, warning.OccurredAt);
        Assert.Equal(CaseExceptions.None, (await ReadAsync(@case.Id)).Exceptions);
    }

    [Fact]
    public async Task Passed_deadline_flags_the_case_overdue()
    {
        var @case = await SeedAsync();
        _clock.Set(@case.DueAt!.Value.AddTicks(1));
        Assert.Equal(1, await SweepAsync());
        Assert.True((await ReadAsync(@case.Id)).Exceptions.HasFlag(CaseExceptions.Overdue));
    }

    [Fact]
    public async Task Passed_deadline_escalates_to_the_facility_lead_then_the_coordinator()
    {
        var @case = await SeedAsync();
        _clock.Set(@case.DueAt!.Value.AddTicks(1));
        await SweepAsync();
        var first = Assert.Single(await RaisedAsync<EscalationRaised>());
        Assert.Equal("facility-lead", first.EscalatedTo.Role);
        Assert.Equal("facility-lead:SYN-FAC-A", first.EscalatedTo.Reference);
        Assert.True(first.EscalatedTo.IsQueue);

        var second = DomainTestData.Pathway.EscalationLadder.Single(s => s.Level == 2);
        _clock.Set(@case.DueAt.Value + second.AfterOverdue);
        Assert.Equal(1, await SweepAsync());
        var raised = (await RaisedAsync<EscalationRaised>()).OrderBy(e => e.Level).ToList();
        Assert.Equal([1, 2], raised.Select(e => e.Level));
        Assert.Equal("coordinator", raised[1].EscalatedTo.Role);
        Assert.Equal(@case.FacilityId, raised[1].EscalatedTo.FacilityId);
        Assert.All(raised, e => Assert.NotEqual(Guid.Empty, e.EscalationId));
    }

    [Fact]
    public async Task Sweep_twice_at_the_same_instant_changes_nothing_the_second_time()
    {
        var @case = await SeedAsync();
        _clock.Set(@case.DueAt!.Value.AddDays(3));
        Assert.Equal(1, await SweepAsync());
        var version = (await ReadAsync(@case.Id)).Version;
        Assert.Equal(0, await SweepAsync());
        Assert.Equal(version, (await ReadAsync(@case.Id)).Version);
        Assert.Equal(2, (await RaisedAsync<EscalationRaised>()).Count);
        Assert.Single(await DeadlineEventsAsync());
    }

    [Fact]
    public async Task Escalation_does_not_change_owner_stage_or_urgency()
    {
        var @case = await SeedAsync(DomainTestData.InFollowUp(_clock));
        _clock.Set(@case.DueAt!.Value.AddDays(2));
        await SweepAsync();
        var after = await ReadAsync(@case.Id);
        Assert.Equal(@case.Owner, after.Owner);
        Assert.Equal(@case.Stage, after.Stage);
        Assert.Equal(@case.Urgency, after.Urgency);
        Assert.Equal(@case.NextAction, after.NextAction);
        Assert.Equal(@case.DueAt, after.DueAt);
        Assert.True(after.IsEscalated);
    }

    [Fact]
    public async Task Closed_cases_are_ignored()
    {
        var @case = await SeedAsync(DomainTestData.Closed(_clock));
        _clock.Advance(TimeSpan.FromDays(10));
        Assert.Equal(0, await SweepAsync());
        Assert.Empty(await DeadlineEventsAsync());
        Assert.Empty(await RaisedAsync<EscalationRaised>());
        Assert.Equal(@case.Version, (await ReadAsync(@case.Id)).Version);
    }

    [Theory]
    [InlineData(NotificationStatus.Sent, true)]
    [InlineData(NotificationStatus.Delivered, true)]
    [InlineData(NotificationStatus.Acknowledged, false)]
    [InlineData(NotificationStatus.Understood, false)]
    public async Task Unacknowledged_notification_past_deadline_flags_unacknowledged(NotificationStatus status, bool expected)
    {
        var @case = DomainTestData.InFollowUp(_clock);
        if (status != NotificationStatus.Sent)
        {
            @case.Notification!.Advance(status, _clock.UtcNow, "SYN-COMM-1");
        }

        await SeedAsync(@case);
        _clock.Set(@case.DueAt!.Value.AddTicks(1));
        await SweepAsync();
        Assert.Equal(expected, (await ReadAsync(@case.Id)).Exceptions.HasFlag(CaseExceptions.Unacknowledged));
    }

    [Fact]
    public async Task Each_change_is_saved_as_a_DeadlineElapsed_event_and_audited()
    {
        var @case = await SeedAsync();
        SetWarningTime(@case);
        await SweepAsync();
        _clock.Set(@case.DueAt!.Value.AddTicks(1));
        await SweepAsync();
        var events = await DeadlineEventsAsync();
        Assert.Equal(2, events.Count);
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLoopDbContext>();
        var audits = await db.AuditEvents.Where(a => a.EventType == CaseEventType.DeadlineElapsed).ToListAsync();
        Assert.Equal(2, audits.Count);
        foreach (var applied in events)
        {
            Assert.Equal("careloop", applied.SourceSystem);
            Assert.Equal(SourceEventDisposition.Applied, applied.Disposition);
            Assert.Equal(applied.OccurredAt, applied.ReceivedAt);
            var audit = audits.Single(a => a.SourceEventId == applied.Id);
            Assert.Equal("careloop", audit.ActorReference);
            Assert.Equal("system", audit.ActorRole);
            Assert.NotNull(audit.PriorProjectionJson);
            Assert.NotNull(audit.NewProjectionJson);
            Assert.Equal(applied.DispositionReason, audit.Reason);
        }

        var outbox = await db.OutboxMessages.Include(m => m.Deliveries).ToListAsync();
        Assert.Equal(2, outbox.Count);
        Assert.All(outbox, m => Assert.Equal(["FhirPublisher", "NotificationRouter"], m.Deliveries.Select(d => d.Subscriber).Order()));
    }

    [Fact]
    public async Task Acknowledging_level_one_does_not_stop_level_two_and_its_notice_carries_the_acknowledgement()
    {
        var @case = await SeedAsync();
        _clock.Set(@case.DueAt!.Value.AddTicks(1));
        await SweepAsync();
        var acknowledgedAt = _clock.UtcNow;
        await ChangeAsync(@case.Id, c => c.AcknowledgeEscalation(c.Escalations.Single().Id,
            new Actor { Reference = "SYN-LEAD-1", Role = "facility-lead", FacilityId = c.FacilityId }, acknowledgedAt),
            CaseEventType.EscalationAcknowledged);
        _clock.Set(@case.DueAt.Value + DomainTestData.Pathway.EscalationLadder.Single(s => s.Level == 2).AfterOverdue);
        Assert.Equal(1, await SweepAsync());
        var notice = (await RaisedAsync<EscalationRaised>()).Single(e => e.Level == 2);
        Assert.Equal("facility-lead", notice.AcknowledgedByRole);
        Assert.Equal(acknowledgedAt, notice.AcknowledgedAt);
        Assert.True((await ReadAsync(@case.Id)).Exceptions.HasFlag(CaseExceptions.Overdue));
    }

    [Fact]
    public async Task New_deadline_gets_a_fresh_warning_and_escalation_ladder()
    {
        var @case = await SeedAsync();
        SetWarningTime(@case);
        await SweepAsync();
        _clock.Set(@case.DueAt!.Value.AddDays(2));
        await SweepAsync();
        await ChangeAsync(@case.Id, c =>
        {
            c.ResolveEscalations(_clock.UtcNow);
            c.SetNextAction("review-result",
                DeadlinePolicy.ComputeDueAt("review-result", c.Urgency, DomainTestData.Pathway, _clock), _clock.UtcNow);
        });
        var updated = await ReadAsync(@case.Id);
        SetWarningTime(updated);
        Assert.Equal(1, await SweepAsync());
        Assert.Equal(2, (await RaisedAsync<DeadlineApproaching>()).Count);
        _clock.Set(updated.DueAt!.Value.AddTicks(1));
        Assert.Equal(1, await SweepAsync());
        Assert.Equal([1, 1, 2], (await RaisedAsync<EscalationRaised>()).Select(e => e.Level).Order());
        Assert.Equal(0, await SweepAsync());
    }

    [Fact]
    public async Task Future_cases_and_cases_without_deadlines_are_ignored()
    {
        await SeedAsync();
        await SeedAsync(TestData.NewCase(_clock.UtcNow, "SYN-RESULT-NO-DEADLINE"));
        Assert.Equal(0, await SweepAsync());
        Assert.Empty(await DeadlineEventsAsync());
    }

    [Fact]
    public async Task A_bad_pathway_is_reported_without_blocking_other_cases()
    {
        var bad = new Case(Guid.NewGuid(), "SYN-HIS", "SYN-RESULT-BAD", "SYN-PT-1", "SYN-UNKNOWN", "SYN-FAC-A",
            TestData.Clinician(), _clock.UtcNow);
        bad.SetNextAction("review-result", _clock.UtcNow.AddMinutes(1), _clock.UtcNow);
        await SeedAsync(bad);
        var valid = await SeedAsync();
        _clock.Set(valid.DueAt!.Value.AddTicks(1));
        Assert.Equal(1, await SweepAsync());
        Assert.Equal(CaseExceptions.None, (await ReadAsync(bad.Id)).Exceptions);
        Assert.True((await ReadAsync(valid.Id)).Exceptions.HasFlag(CaseExceptions.Overdue));
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Error && e.Exception is KeyNotFoundException);
    }

    [Fact]
    public async Task Concurrent_case_change_rolls_back_the_sweep_and_is_rechecked_on_the_new_deadline()
    {
        var @case = await SeedAsync();
        SetWarningTime(@case);
        _beforeDeadlineSave = () => ChangeAsync(@case.Id, c => c.SetNextAction("call-patient",
            DeadlinePolicy.ComputeDueAt("call-patient", c.Urgency, DomainTestData.Pathway, _clock), _clock.UtcNow));

        Assert.Equal(0, await SweepAsync());
        Assert.Empty(await RaisedAsync<DeadlineApproaching>());
        Assert.Empty(await DeadlineEventsAsync());
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Warning && e.Exception is DbUpdateConcurrencyException);
        var updated = await ReadAsync(@case.Id);
        Assert.Equal("call-patient", updated.NextAction);
        SetWarningTime(updated);
        Assert.Equal(1, await SweepAsync());
        Assert.Equal("call-patient", Assert.Single(await RaisedAsync<DeadlineApproaching>()).NextAction);
        Assert.Equal(0, await SweepAsync());
    }

    [Fact]
    public async Task Cancellation_propagates_from_an_explicit_sweep()
    {
        await SeedAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _worker.SweepAsync(cancellation.Token));
    }

    [Fact]
    public async Task Background_loop_sweeps_immediately_and_stops_cleanly()
    {
        var @case = await SeedAsync();
        _clock.Set(@case.DueAt!.Value.AddTicks(1));
        await _worker.StartAsync(CancellationToken.None);
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while ((await DeadlineEventsAsync()).Count == 0)
            {
                await Task.Delay(10, timeout.Token);
            }
        }
        finally
        {
            await _worker.StopAsync(CancellationToken.None);
        }

        Assert.Single(await DeadlineEventsAsync());
    }

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("-00:00:01")]
    public void Invalid_polling_interval_is_refused(string interval)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["DeadlineWorker:Interval"] = interval }).Build();
        var services = new ServiceCollection().AddDeadlines(configuration);
        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<DeadlineWorkerOptions>>().Value);
    }

    private DeadlineWorker NewWorker() => new(_services.GetRequiredService<IServiceScopeFactory>(),
        Options.Create(new DeadlineWorkerOptions { Interval = TimeSpan.FromHours(1) }), _clock, _logger);

    private Task<int> SweepAsync() => _worker.SweepAsync(CancellationToken.None);

    private void SetWarningTime(Case @case) => _clock.Set(@case.DueAt!.Value
        - DeadlinePolicy.FindDeadline(@case.NextAction!, @case.Urgency, DomainTestData.Pathway).WarnBefore);

    private async Task<Case> SeedAsync(Case? @case = null)
    {
        @case ??= DomainTestData.InReviewPending(_clock);
        @case.ClearDomainEvents();
        using var scope = _services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ICaseRepository>().SaveAsync(@case,
            TestData.AppliedEvent(@case, Guid.NewGuid().ToString("N"), _clock.UtcNow), CancellationToken.None);
        return @case;
    }

    private async Task<Case> ReadAsync(Guid id)
    {
        using var scope = _services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<ICaseRepository>().GetAsync(id, CancellationToken.None))!;
    }

    private async Task ChangeAsync(Guid id, Action<Case> change, CaseEventType type = CaseEventType.ClinicalReviewCompleted)
    {
        using var scope = _services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICaseRepository>();
        var @case = (await repository.GetAsync(id, CancellationToken.None))!;
        change(@case);
        await repository.SaveAsync(@case, TestData.AppliedEvent(@case, Guid.NewGuid().ToString("N"), _clock.UtcNow, type),
            CancellationToken.None);
    }

    private async Task<List<SourceEvent>> DeadlineEventsAsync()
    {
        using var scope = _services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CareLoopDbContext>().SourceEvents.AsNoTracking()
            .Where(e => e.Type == CaseEventType.DeadlineElapsed).ToListAsync();
    }

    private async Task<List<T>> RaisedAsync<T>() where T : IDomainEvent
    {
        using var scope = _services.CreateScope();
        var payloads = await scope.ServiceProvider.GetRequiredService<CareLoopDbContext>().OutboxMessages.AsNoTracking()
            .Where(m => m.EventType == typeof(T).Name).Select(m => m.PayloadJson).ToListAsync();
        return payloads.Select(p => JsonSerializer.Deserialize<T>(p, JsonOptions)!).ToList();
    }

    private sealed class InterceptingRepository(EfCaseRepository inner, DeadlineWorkerTests tests) : ICaseRepository
    {
        public Task<Case?> GetAsync(Guid caseId, CancellationToken ct) => inner.GetAsync(caseId, ct);
        public Task<Case?> FindBySourceAsync(string sourceSystem, string sourceResultId, CancellationToken ct) =>
            inner.FindBySourceAsync(sourceSystem, sourceResultId, ct);
        public Task<bool> EventExistsAsync(string sourceSystem, string sourceEventId, CancellationToken ct) =>
            inner.EventExistsAsync(sourceSystem, sourceEventId, ct);

        public async Task SaveAsync(Case @case, SourceEvent appliedEvent, CancellationToken ct)
        {
            if (appliedEvent.Type == CaseEventType.DeadlineElapsed && tests._beforeDeadlineSave is { } beforeSave)
            {
                tests._beforeDeadlineSave = null;
                await beforeSave();
            }

            await inner.SaveAsync(@case, appliedEvent, ct);
        }
    }

    private sealed class TestLogger : ILogger<DeadlineWorker>
    {
        public List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, exception));
    }
}

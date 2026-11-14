using CareLoop.Api.Features.Outbox;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Api.Infrastructure.Persistence.Outbox;
using CareLoop.Domain.Events.DomainEvents;
using CareLoop.Domain.Time;
using CareLoop.Tests.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CareLoop.Tests.Api.Outbox;

/// <summary>
/// Each subscriber is delivered to and retried independently.
/// </summary>
public sealed class OutboxDispatcherTests : IDisposable
{
    private const int MaxAttempts = 3;

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly FakeClock _clock = new();
    private readonly RecordingSubscriber _first = new("First");
    private readonly RecordingSubscriber _second = new("Second");
    private readonly ServiceProvider _services;
    private readonly OutboxDispatcher _dispatcher;

    public OutboxDispatcherTests()
    {
        _connection.Open();
        var services = new ServiceCollection();
        services.AddDbContext<CareLoopDbContext>(o => o.UseSqlite(_connection).ReplaceService<IModelCustomizer, SqliteDateTimeOffsetCustomizer>());
        services.AddSingleton<IDomainEventSubscriber>(_first);
        services.AddSingleton<IDomainEventSubscriber>(_second);
        _services = services.BuildServiceProvider();

        using (var scope = _services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<CareLoopDbContext>().Database.EnsureCreated();
        }

        _dispatcher = new OutboxDispatcher(
            _services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new OutboxOptions { MaxAttempts = MaxAttempts }),
            _clock,
            NullLogger<OutboxDispatcher>.Instance);
    }

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Event_is_delivered_to_every_subscriber()
    {
        var raised = await StoreEventAsync();

        var delivered = await _dispatcher.DispatchPendingAsync(CancellationToken.None);

        Assert.Equal(2, delivered);
        var first = Assert.IsType<CallRequested>(Assert.Single(_first.Received));
        Assert.Equal(raised.EventId, first.EventId);
        Assert.Equal(raised.CurrentOwner, first.CurrentOwner);
        Assert.Equal(raised.DueAt, first.DueAt);
        Assert.Equal(raised.EventId, Assert.Single(_second.Received).EventId);
        Assert.All(await DeliveriesAsync(), d => Assert.Equal(OutboxDeliveryStatus.Delivered, d.Status));
    }

    [Fact]
    public async Task Failing_subscriber_does_not_block_the_others()
    {
        _first.FailuresLeft = int.MaxValue;
        await StoreEventAsync();

        var delivered = await _dispatcher.DispatchPendingAsync(CancellationToken.None);

        Assert.Equal(1, delivered);
        Assert.Single(_second.Received);
        var deliveries = await DeliveriesAsync();
        Assert.Equal(OutboxDeliveryStatus.Pending, deliveries.Single(d => d.Subscriber == "First").Status);
        Assert.Equal(OutboxDeliveryStatus.Delivered, deliveries.Single(d => d.Subscriber == "Second").Status);
    }

    [Fact]
    public async Task Failing_subscriber_is_retried()
    {
        _first.FailuresLeft = 1;
        await StoreEventAsync();

        await _dispatcher.DispatchPendingAsync(CancellationToken.None);
        _clock.Advance(OutboxDispatcher.MaxBackOff);
        await _dispatcher.DispatchPendingAsync(CancellationToken.None);

        Assert.Single(_first.Received);
        var first = (await DeliveriesAsync()).Single(d => d.Subscriber == "First");
        Assert.Equal(OutboxDeliveryStatus.Delivered, first.Status);
        Assert.Equal(2, first.Attempts);
        Assert.Null(first.LastError);
    }

    [Fact]
    public async Task Delivered_subscriber_is_not_called_again_on_retry()
    {
        _first.FailuresLeft = 1;
        await StoreEventAsync();

        await _dispatcher.DispatchPendingAsync(CancellationToken.None);
        _clock.Advance(OutboxDispatcher.MaxBackOff);
        await _dispatcher.DispatchPendingAsync(CancellationToken.None);
        await _dispatcher.DispatchPendingAsync(CancellationToken.None);

        Assert.Single(_second.Received);
        Assert.Single(_first.Received);
    }

    [Fact]
    public async Task Delivery_is_parked_after_the_attempt_limit()
    {
        _first.FailuresLeft = int.MaxValue;
        await StoreEventAsync();

        for (var pass = 0; pass < MaxAttempts + 2; pass++)
        {
            await _dispatcher.DispatchPendingAsync(CancellationToken.None);
            _clock.Advance(OutboxDispatcher.MaxBackOff);
        }

        var first = (await DeliveriesAsync()).Single(d => d.Subscriber == "First");
        Assert.Equal(OutboxDeliveryStatus.Parked, first.Status);
        Assert.Equal(MaxAttempts, first.Attempts);
        Assert.Equal(MaxAttempts, _first.Calls);
        Assert.Contains("subscriber failed", first.LastError);
    }

    [Fact]
    public async Task Next_attempt_time_comes_from_the_clock()
    {
        _first.FailuresLeft = int.MaxValue;
        await StoreEventAsync();
        var failedAt = _clock.UtcNow;

        await _dispatcher.DispatchPendingAsync(CancellationToken.None);

        var first = (await DeliveriesAsync()).Single(d => d.Subscriber == "First");
        Assert.Equal(failedAt, first.LastAttemptAt);
        Assert.Equal(failedAt + OutboxDispatcher.BackOff(1), first.NextAttemptAt);

        // Not due yet: nothing is attempted.
        _clock.Advance(OutboxDispatcher.BackOff(1) - TimeSpan.FromMilliseconds(1));
        await _dispatcher.DispatchPendingAsync(CancellationToken.None);
        Assert.Equal(1, _first.Calls);

        // Due: attempted again.
        _clock.Advance(TimeSpan.FromMilliseconds(1));
        await _dispatcher.DispatchPendingAsync(CancellationToken.None);
        Assert.Equal(2, _first.Calls);
    }

    [Fact]
    public async Task Due_deliveries_go_out_oldest_first_and_waiting_ones_are_left()
    {
        _first.FailuresLeft = 1;
        var older = await StoreEventAsync();
        _clock.Advance(TimeSpan.FromMinutes(1));
        await _dispatcher.DispatchPendingAsync(CancellationToken.None);
        _clock.Advance(TimeSpan.FromSeconds(1));
        var newer = await StoreEventAsync("SYN-RESULT-0002");

        // The older event's retry to First is still backing off; only the newer event is due for it.
        await _dispatcher.DispatchPendingAsync(CancellationToken.None);
        Assert.Equal([newer.EventId], _first.Received.Select(e => e.EventId));

        _clock.Advance(OutboxDispatcher.MaxBackOff);
        await _dispatcher.DispatchPendingAsync(CancellationToken.None);
        Assert.Equal([newer.EventId, older.EventId], _first.Received.Select(e => e.EventId));
        Assert.Equal([older.EventId, newer.EventId], _second.Received.Select(e => e.EventId));
    }

    private async Task<CallRequested> StoreEventAsync(string sourceResultId = "SYN-RESULT-0001")
    {
        var @case = TestData.NewCase(_clock.UtcNow, sourceResultId);
        var raised = new CallRequested
        {
            EventId = Guid.NewGuid(),
            CaseId = @case.Id,
            OccurredAt = _clock.UtcNow,
            CurrentOwner = @case.Owner,
            DueAt = _clock.UtcNow.AddHours(2),
        };
        @case.Raise(raised);

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLoopDbContext>();
        var repository = new EfCaseRepository(db, _clock, new OutboxSubscriberRegistry(() => ["First", "Second"]));
        await repository.SaveAsync(@case, TestData.AppliedEvent(@case, Envelopes.NextEventId(), _clock.UtcNow), CancellationToken.None);
        return raised;
    }

    private async Task<List<OutboxDelivery>> DeliveriesAsync()
    {
        using var scope = _services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CareLoopDbContext>().OutboxDeliveries.AsNoTracking().ToListAsync();
    }

    private sealed class RecordingSubscriber(string name) : IDomainEventSubscriber
    {
        public string Name => name;

        public int FailuresLeft { get; set; }

        public int Calls { get; private set; }

        public List<IDomainEvent> Received { get; } = [];

        public Task HandleAsync(IDomainEvent domainEvent, CancellationToken ct)
        {
            Calls++;
            if (FailuresLeft > 0)
            {
                FailuresLeft--;
                throw new InvalidOperationException("The subscriber failed.");
            }

            Received.Add(domainEvent);
            return Task.CompletedTask;
        }
    }
}

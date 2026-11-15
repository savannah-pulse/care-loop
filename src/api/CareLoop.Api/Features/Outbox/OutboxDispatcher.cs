using System.Text.Json;
using System.Text.Json.Serialization;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Api.Infrastructure.Persistence.Outbox;
using CareLoop.Domain.Events.DomainEvents;
using CareLoop.Domain.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Features.Outbox;

/// <summary>
/// Background service that delivers stored domain events to every subscriber.
/// </summary>
/// <remarks>
/// Everything outbound goes through the outbox so that a message is sent if and only if the case
/// change that caused it was committed. Polls for outbox messages with a pending
/// <c>OutboxDelivery</c>, deserializes each to its domain event, calls each
/// <see cref="IDomainEventSubscriber"/> separately and records success or failure per subscriber.
/// A failing subscriber is retried with back-off and never blocks the others; after
/// <see cref="OutboxOptions.MaxAttempts"/> failures its delivery is parked for a person to look at.
/// <para>
/// Several API instances may run a dispatcher against one database. Each delivery is claimed with a single
/// conditional update before its subscriber runs (<see cref="OutboxDelivery.LeaseOwner"/>,
/// <see cref="OutboxDelivery.LeasedUntil"/>), so only one instance delivers it at a time, and only the claim owner
/// records the outcome. A claim left by an instance that stopped lapses after <see cref="OutboxOptions.LeaseDuration"/>
/// and the delivery is picked up again, so delivery is at least once and subscribers stay idempotent.
/// </para>
/// </remarks>
public sealed class OutboxDispatcher : BackgroundService
{
    /// <summary>The longest wait between two attempts of one delivery.</summary>
    public static readonly TimeSpan MaxBackOff = TimeSpan.FromMinutes(10);

    private const int BatchSize = 100;

    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private static readonly IReadOnlyDictionary<string, Type> EventTypes = typeof(IDomainEvent).Assembly
        .GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IDomainEvent).IsAssignableFrom(t))
        .ToDictionary(t => t.Name, StringComparer.Ordinal);

    private readonly string _instance = $"{Environment.MachineName}/{Guid.NewGuid():N}";
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<OutboxOptions> _options;
    private readonly IClock _clock;
    private readonly ILogger<OutboxDispatcher> _logger;

    /// <summary>
    /// Creates the dispatcher.
    /// </summary>
    /// <param name="scopes">Creates a dependency-injection scope per dispatch pass, for the database context and subscribers.</param>
    /// <param name="options">Polling interval and retry limit.</param>
    /// <param name="clock">Supplies attempt timestamps and retry times.</param>
    /// <param name="logger">Logs delivery failures without patient detail.</param>
    public OutboxDispatcher(
        IServiceScopeFactory scopes,
        IOptions<OutboxOptions> options,
        IClock clock,
        ILogger<OutboxDispatcher> logger)
    {
        _scopes = scopes;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// How long to wait before the next attempt after <paramref name="attempts"/> failed ones.
    /// </summary>
    /// <param name="attempts">Failed attempts so far, at least 1.</param>
    /// <returns>Two seconds doubled per failed attempt, capped at <see cref="MaxBackOff"/>.</returns>
    public static TimeSpan BackOff(int attempts)
    {
        var seconds = Math.Pow(2, Math.Clamp(attempts, 1, 30));
        return seconds >= MaxBackOff.TotalSeconds ? MaxBackOff : TimeSpan.FromSeconds(seconds);
    }

    /// <summary>
    /// Runs the polling loop until the host stops.
    /// </summary>
    /// <param name="stoppingToken">Signals that the host is shutting down.</param>
    /// <returns>A task that completes when the loop has stopped.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A failed pass (for example the database being briefly unavailable) must not stop the host.
                _logger.LogError(ex, "Outbox dispatch pass failed; retrying after the poll interval");
            }

            try
            {
                await Task.Delay(_options.Value.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Delivers every pending outbox message once to each subscriber that has not yet handled it.
    /// </summary>
    /// <param name="ct">Cancels the pass.</param>
    /// <returns>The number of deliveries that succeeded in this pass.</returns>
    /// <remarks>Exposed separately from the loop so tests can run a single pass with a controlled clock.</remarks>
    public async Task<int> DispatchPendingAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLoopDbContext>();
        var now = _clock.UtcNow;

        // Due, unclaimed deliveries, oldest message first, one batch. Filtering, ordering and the limit run in the
        // database, so deliveries still backing off or claimed by another instance are never read.
        var batch = await (
                from d in db.OutboxDeliveries.AsNoTracking()
                join m in db.OutboxMessages.AsNoTracking() on d.OutboxMessageId equals m.Id
                where d.Status == OutboxDeliveryStatus.Pending
                    && (d.NextAttemptAt == null || d.NextAttemptAt <= now)
                    && (d.LeasedUntil == null || d.LeasedUntil <= now)
                orderby m.CreatedAt, m.Id
                select new { Delivery = d, Message = m })
            .Take(BatchSize)
            .ToListAsync(ct);

        var delivered = 0;
        foreach (var item in batch)
        {
            ct.ThrowIfCancellationRequested();
            if (!await ClaimAsync(db, item.Delivery, ct))
            {
                // Another instance claimed it between the read and now.
                continue;
            }

            if (await DeliverAsync(item.Delivery, item.Message, ct))
            {
                delivered++;
            }

            await RecordAsync(db, item.Delivery, ct);
        }

        return delivered;
    }

    /// <summary>
    /// Claims a delivery for this instance with one conditional update, which only one instance can win.
    /// </summary>
    private async Task<bool> ClaimAsync(CareLoopDbContext db, OutboxDelivery delivery, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var until = now + _options.Value.LeaseDuration;
        var claimed = await db.OutboxDeliveries
            .Where(d => d.Id == delivery.Id
                && d.Status == OutboxDeliveryStatus.Pending
                && (d.LeasedUntil == null || d.LeasedUntil <= now))
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.LeaseOwner, _instance)
                .SetProperty(d => d.LeasedUntil, until), ct);
        return claimed == 1;
    }

    /// <summary>
    /// Records the outcome and releases the claim, only while this instance still holds it.
    /// </summary>
    /// <remarks>
    /// When the claim lapsed and another instance took the delivery over, that instance records the outcome instead,
    /// so two instances never overwrite each other's bookkeeping.
    /// </remarks>
    private async Task RecordAsync(CareLoopDbContext db, OutboxDelivery delivery, CancellationToken ct)
    {
        var recorded = await db.OutboxDeliveries
            .Where(d => d.Id == delivery.Id && d.LeaseOwner == _instance)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Status, delivery.Status)
                .SetProperty(d => d.Attempts, delivery.Attempts)
                .SetProperty(d => d.LastAttemptAt, delivery.LastAttemptAt)
                .SetProperty(d => d.NextAttemptAt, delivery.NextAttemptAt)
                .SetProperty(d => d.DeliveredAt, delivery.DeliveredAt)
                .SetProperty(d => d.LastError, delivery.LastError)
                .SetProperty(d => d.LeaseOwner, (string?)null)
                .SetProperty(d => d.LeasedUntil, (DateTimeOffset?)null), ct);
        if (recorded == 0)
        {
            _logger.LogWarning(
                "Outbox delivery {DeliveryId} to {Subscriber} was taken over by another instance; its outcome there stands",
                delivery.Id, delivery.Subscriber);
        }
    }

    private async Task<bool> DeliverAsync(OutboxDelivery delivery, OutboxMessage message, CancellationToken ct)
    {
        var attemptedAt = _clock.UtcNow;
        delivery.Attempts++;
        delivery.LastAttemptAt = attemptedAt;

        try
        {
            var domainEvent = Deserialize(message);

            // A scope per delivery: one subscriber's broken state (for example a failed database context) never leaks into another's.
            using var scope = _scopes.CreateScope();
            var subscriber = scope.ServiceProvider.GetServices<IDomainEventSubscriber>()
                .FirstOrDefault(s => string.Equals(s.Name, delivery.Subscriber, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"No subscriber named '{delivery.Subscriber}' is registered.");

            await subscriber.HandleAsync(domainEvent, ct);

            delivery.Status = OutboxDeliveryStatus.Delivered;
            delivery.DeliveredAt = _clock.UtcNow;
            delivery.NextAttemptAt = null;
            delivery.LastError = null;
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutting down is not the subscriber's fault. The claim is left to lapse, and the attempt is not counted.
            throw;
        }
        catch (Exception ex)
        {
            // Only the exception type and message are kept: subscribers must not put patient detail in exceptions.
            delivery.LastError = $"{ex.GetType().Name}: {ex.Message}";
            if (delivery.Attempts >= _options.Value.MaxAttempts)
            {
                delivery.Status = OutboxDeliveryStatus.Parked;
                delivery.NextAttemptAt = null;
                _logger.LogError(
                    "Outbox delivery of {EventType} {MessageId} to {Subscriber} parked after {Attempts} attempts: {Error}",
                    message.EventType, message.Id, delivery.Subscriber, delivery.Attempts, delivery.LastError);
            }
            else
            {
                delivery.NextAttemptAt = attemptedAt + BackOff(delivery.Attempts);
                _logger.LogWarning(
                    "Outbox delivery of {EventType} {MessageId} to {Subscriber} failed (attempt {Attempts}); retrying at {NextAttemptAt}: {Error}",
                    message.EventType, message.Id, delivery.Subscriber, delivery.Attempts, delivery.NextAttemptAt, delivery.LastError);
            }

            return false;
        }
    }

    private static IDomainEvent Deserialize(OutboxMessage message)
    {
        if (!EventTypes.TryGetValue(message.EventType, out var type))
        {
            throw new InvalidOperationException($"Outbox message {message.Id} has unknown event type '{message.EventType}'.");
        }

        return (IDomainEvent?)JsonSerializer.Deserialize(message.PayloadJson, type, SerializerOptions)
            ?? throw new InvalidOperationException($"Outbox message {message.Id} has an empty payload.");
    }

    /// <summary>Must match the options <c>EfCaseRepository</c> serializes with: web defaults, enums as names.</summary>
    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

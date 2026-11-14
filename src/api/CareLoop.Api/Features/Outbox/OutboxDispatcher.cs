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

        // Due-time filtering and ordering happen in memory: SQLite cannot compare DateTimeOffset values.
        var pending = await db.OutboxDeliveries
            .Where(d => d.Status == OutboxDeliveryStatus.Pending)
            .ToListAsync(ct);
        var due = pending.Where(d => d.NextAttemptAt is null || d.NextAttemptAt <= now).ToList();
        if (due.Count == 0)
        {
            return 0;
        }

        var messageIds = due.Select(d => d.OutboxMessageId).Distinct().ToList();
        var messages = await db.OutboxMessages.AsNoTracking()
            .Where(m => messageIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, ct);

        var delivered = 0;
        foreach (var delivery in due
            .Where(d => messages.ContainsKey(d.OutboxMessageId))
            .OrderBy(d => messages[d.OutboxMessageId].CreatedAt)
            .ThenBy(d => messages[d.OutboxMessageId].Id)
            .Take(BatchSize))
        {
            ct.ThrowIfCancellationRequested();
            if (await DeliverAsync(delivery, messages[delivery.OutboxMessageId], ct))
            {
                delivered++;
            }

            await db.SaveChangesAsync(ct);
        }

        return delivered;
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
            // Shutting down is not the subscriber's fault: give the attempt back.
            delivery.Attempts--;
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

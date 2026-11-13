using CareLoop.Domain.Time;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Features.Outbox;

/// <summary>
/// Background service that delivers stored domain events to every subscriber.
/// </summary>
/// <remarks>
/// Everything outbound goes through the outbox so that a message is sent if and only if the case
/// change that caused it was committed. Will poll for outbox messages with a pending
/// <c>OutboxDelivery</c>, deserialize each to its domain event, call each
/// <see cref="IDomainEventSubscriber"/> separately and record success or failure per subscriber.
/// A failing subscriber is retried with back-off and never blocks the others.
/// </remarks>
public sealed class OutboxDispatcher : BackgroundService
{
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
    /// Runs the polling loop until the host stops.
    /// </summary>
    /// <param name="stoppingToken">Signals that the host is shutting down.</param>
    /// <returns>A task that completes when the loop has stopped.</returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // TODO(WS-4): loop on PollInterval calling DispatchPendingAsync until cancelled.
        throw new NotImplementedException("WS4: OutboxDispatcher.ExecuteAsync");
    }

    /// <summary>
    /// Delivers every pending outbox message once to each subscriber that has not yet handled it.
    /// </summary>
    /// <param name="ct">Cancels the pass.</param>
    /// <returns>The number of deliveries that succeeded in this pass.</returns>
    /// <remarks>Exposed separately from the loop so tests can run a single pass with a controlled clock.</remarks>
    public Task<int> DispatchPendingAsync(CancellationToken ct)
    {
        // TODO(WS-4): load due OutboxDelivery rows, invoke the subscriber, record Delivered or Failed with next attempt time.
        throw new NotImplementedException("WS4: OutboxDispatcher.DispatchPendingAsync");
    }
}

using CareLoop.Domain.Time;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Worker;

/// <summary>
/// Background service that watches every open case's deadline and warns or escalates.
/// </summary>
/// <remarks>
/// A deadline nobody watches protects nobody. On each sweep the worker will classify every open
/// case with <see cref="CareLoop.Domain.Rules.DeadlinePolicy"/>: an approaching deadline raises
/// <see cref="CareLoop.Domain.Events.DomainEvents.DeadlineApproaching"/> once; a passed deadline
/// flags the case overdue and raises the next step of the pathway's escalation ladder (facility
/// lead, then coordinator). Each change is saved through the single write path as a
/// <see cref="CareLoop.Domain.Events.CaseEventType.DeadlineElapsed"/> event, so it is audited.
/// The worker never changes ownership, urgency or stage, and takes the time only from <see cref="IClock"/>.
/// </remarks>
public sealed class DeadlineWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<DeadlineWorkerOptions> _options;
    private readonly IClock _clock;
    private readonly ILogger<DeadlineWorker> _logger;

    /// <summary>
    /// Creates the worker.
    /// </summary>
    /// <param name="scopes">Creates a dependency-injection scope per sweep, for the repository and database context.</param>
    /// <param name="options">The sweep interval.</param>
    /// <param name="clock">Supplies the current time.</param>
    /// <param name="logger">Logs sweep results without patient detail.</param>
    public DeadlineWorker(
        IServiceScopeFactory scopes,
        IOptions<DeadlineWorkerOptions> options,
        IClock clock,
        ILogger<DeadlineWorker> logger)
    {
        _scopes = scopes;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Runs the sweep loop until the host stops.
    /// </summary>
    /// <param name="stoppingToken">Signals that the host is shutting down.</param>
    /// <returns>A task that completes when the loop has stopped.</returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // TODO(WS-5): loop on Interval calling SweepAsync until cancelled.
        throw new NotImplementedException("WS5: DeadlineWorker.ExecuteAsync");
    }

    /// <summary>
    /// Checks every open case once and applies warnings and escalations that are due.
    /// </summary>
    /// <param name="ct">Cancels the sweep.</param>
    /// <returns>The number of cases changed in this sweep.</returns>
    /// <remarks>Exposed separately from the loop so tests and the simulation can run one sweep at a controlled time. Running it twice at the same instant must change nothing the second time.</remarks>
    public Task<int> SweepAsync(CancellationToken ct)
    {
        // TODO(WS-5): query open cases with a due time, classify, raise events, save via ICaseRepository.
        throw new NotImplementedException("WS5: DeadlineWorker.SweepAsync");
    }
}

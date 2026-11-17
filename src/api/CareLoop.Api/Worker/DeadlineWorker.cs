using System.Text.Json;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Events.DomainEvents;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Rules;
using CareLoop.Domain.Time;
using Microsoft.EntityFrameworkCore;
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
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deadline sweep failed; retrying after the sweep interval");
            }

            try
            {
                await Task.Delay(_options.Value.Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Checks every open case once and applies warnings and escalations that are due.
    /// </summary>
    /// <param name="ct">Cancels the sweep.</param>
    /// <returns>The number of cases changed in this sweep.</returns>
    /// <remarks>
    /// Exposed separately from the loop so tests and the simulation can run one sweep at a controlled
    /// time. Running it twice at the same instant must change nothing the second time. Warning identity
    /// is stored with the source event, per case, next action and due time, so restarting cannot repeat
    /// it. A late sweep catches up all due ladder steps in order. Case checks use isolated scopes;
    /// failed checks are logged and retried next sweep, and optimistic concurrency prevents a stale
    /// check from overwriting newer coordination state.
    /// </remarks>
    public async Task<int> SweepAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLoopDbContext>();
        var pathways = scope.ServiceProvider.GetRequiredService<IPathwayProvider>().GetAll();
        var horizon = _clock.UtcNow + pathways.SelectMany(p => p.Deadlines).Select(d => d.WarnBefore)
            .DefaultIfEmpty(TimeSpan.Zero).Max();

        // Only the id and due time are read here; a full aggregate is loaded just for cases that can need work.
        var candidates = await db.Cases.AsNoTracking()
            .Where(c => c.Stage != CaseStage.Closed && c.DueAt != null)
            .Select(c => new { c.Id, c.DueAt })
            .ToListAsync(ct);
        var caseIds = candidates.Where(c => c.DueAt <= horizon).Select(c => c.Id).ToList();

        var changed = 0;
        foreach (var caseId in caseIds)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // A failed save must not leave tracked changes that leak into another case's transaction.
                using var caseScope = _scopes.CreateScope();
                if (await CheckCaseAsync(caseScope.ServiceProvider, caseId, ct))
                {
                    changed++;
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Case {CaseId} changed during the deadline sweep; checking again next sweep", caseId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Deadline check failed for case {CaseId}; checking again next sweep", caseId);
            }
        }

        return changed;
    }

    private async Task<bool> CheckCaseAsync(IServiceProvider services, Guid caseId, CancellationToken ct)
    {
        var repository = services.GetRequiredService<ICaseRepository>();
        var @case = await repository.GetAsync(caseId, ct);
        if (@case is null || @case.IsClosed || @case.DueAt is not { } dueAt)
        {
            return false;
        }

        var pathway = services.GetRequiredService<IPathwayProvider>().Get(@case.PathwayId);
        var status = DeadlinePolicy.Classify(@case, pathway, _clock);
        var now = _clock.UtcNow;
        var sourceEventId = Guid.NewGuid().ToString("N");
        var levels = new List<int>();
        var flags = CaseExceptions.None;

        if (status == DeadlineStatus.Approaching)
        {
            sourceEventId = $"deadline-warning/{@case.Id:N}/{dueAt.UtcTicks}/{@case.NextAction!.ToLowerInvariant()}";
            if (await repository.EventExistsAsync("careloop", sourceEventId, ct))
            {
                return false;
            }

            @case.Raise(new DeadlineApproaching
            {
                EventId = Guid.NewGuid(),
                CaseId = @case.Id,
                OccurredAt = now,
                CurrentOwner = @case.Owner,
                NextAction = @case.NextAction,
                DueAt = dueAt,
            });
        }
        else if (status == DeadlineStatus.Overdue)
        {
            flags = CaseExceptions.Overdue;
            if (string.Equals(@case.NextAction, DeadlinePolicy.AcknowledgeNotificationAction, StringComparison.OrdinalIgnoreCase)
                && @case.Notification is { } notification && !notification.IsComplete())
            {
                flags |= CaseExceptions.Unacknowledged;
            }

            flags &= ~@case.Exceptions;
            if (flags != CaseExceptions.None)
            {
                @case.FlagException(flags, now);
            }

            while (DeadlinePolicy.NextEscalationStep(@case, pathway, _clock) is { } step)
            {
                now = _clock.UtcNow;
                @case.Escalate(new Escalation
                {
                    Level = step.Level,
                    EscalatedTo = new CaseOwner
                    {
                        Reference = $"{step.EscalateToRole}:{@case.FacilityId}",
                        Role = step.EscalateToRole,
                        FacilityId = @case.FacilityId,
                        IsQueue = true,
                    },
                    RaisedAt = now,
                    Reason = $"Next action '{@case.NextAction}' was due at {dueAt:O}.",
                });
                levels.Add(step.Level);
            }

            if (flags == CaseExceptions.None && levels.Count == 0)
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        await repository.SaveAsync(@case, new SourceEvent
        {
            Id = Guid.NewGuid(),
            SourceSystem = EventOrigins.CareLoop,
            Origin = EventOrigins.CareLoop,
            SourceEventId = sourceEventId,
            Type = CaseEventType.DeadlineElapsed,
            CaseId = @case.Id,
            OccurredAt = now,
            ReceivedAt = now,
            PayloadJson = JsonSerializer.Serialize(new
            {
                nextAction = @case.NextAction,
                dueAt,
                status = status.ToString(),
                flags = flags.ToString(),
                escalationLevels = levels,
            }),
            Disposition = SourceEventDisposition.Applied,
            DispositionReason = status == DeadlineStatus.Approaching
                ? "Deadline warning raised"
                : "Overdue deadline checked; exception flags or escalation steps raised",
        }, ct);
        return true;
    }
}

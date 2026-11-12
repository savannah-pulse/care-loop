using CareLoop.Application.Queries;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Stages;
using Microsoft.EntityFrameworkCore;

namespace CareLoop.Infrastructure.Persistence;

internal sealed class CaseQueries(CareLoopDbContext db) : ICaseQueries
{
    public async Task<PagedResult<CaseSummaryDto>> ListCasesAsync(CaseListQuery query, CancellationToken cancellationToken)
    {
        var cases = db.Cases.AsNoTracking();

        if (query.Stage is { } stage)
        {
            cases = cases.Where(c => c.Stage == stage);
        }

        if (query.HasOpenExceptions is { } hasOpen)
        {
            cases = cases.Where(c => c.Exceptions.Any(e => e.Status == ExceptionStatus.Open) == hasOpen);
        }

        if (query.OwnerKind is { } ownerKind)
        {
            cases = cases.Where(c => c.OwnerKind == ownerKind);
        }

        if (!string.IsNullOrWhiteSpace(query.OwnerId))
        {
            cases = cases.Where(c => c.OwnerId == query.OwnerId);
        }

        var page = query.NormalizedPage;
        var pageSize = query.NormalizedPageSize;
        var total = await cases.CountAsync(cancellationToken);
        var rows = await Summaries(cases
                .OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id)
                .Skip((page - 1) * pageSize).Take(pageSize))
            .ToListAsync(cancellationToken);

        return new PagedResult<CaseSummaryDto>(rows.Select(ToDto).ToList(), page, pageSize, total);
    }

    public async Task<CaseSummaryDto?> GetCaseAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await Summaries(db.Cases.AsNoTracking().Where(c => c.Id == id)).FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : ToDto(row);
    }

    public async Task<IReadOnlyList<TimelineEntryDto>?> GetTimelineAsync(Guid caseId, CancellationToken cancellationToken)
    {
        var events = await db.Events.AsNoTracking()
            .Where(e => e.CaseId == caseId)
            .OrderBy(e => e.CaseSequence)
            .ToListAsync(cancellationToken);

        if (events.Count == 0 && !await db.Cases.AnyAsync(c => c.Id == caseId, cancellationToken))
        {
            return null;
        }

        var transitions = (await db.StageTransitions.AsNoTracking()
                .Where(t => t.CaseId == caseId).ToListAsync(cancellationToken))
            .ToDictionary(t => t.EventId);

        return events.Select(e => new TimelineEntryDto(
            e.CaseSequence, e.Id, e.EventType, e.SourceSystem, e.SourceEventId, e.ActorKind, e.ActorId, e.ActorRole,
            e.Disposition, e.OccurredAt, e.ReceivedAt,
            transitions.TryGetValue(e.Id, out var t)
                ? new StageTransitionDto(
                    t.FromStage is { } from ? ToStageDto(from) : null, ToStageDto(t.ToStage),
                    t.ActorId, t.ActorKind, t.OccurredAt)
                : null)).ToList();
    }

    public async Task<PagedResult<CaseExceptionDto>> ListExceptionsAsync(ExceptionListQuery query, CancellationToken cancellationToken)
    {
        var exceptions = db.CaseExceptions.AsNoTracking();
        if (query.Status is { } status)
        {
            exceptions = exceptions.Where(e => e.Status == status);
        }

        var page = query.NormalizedPage;
        var pageSize = query.NormalizedPageSize;
        var total = await exceptions.CountAsync(cancellationToken);
        var rows = await exceptions
            .OrderBy(e => e.RaisedAt).ThenBy(e => e.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(e => new CaseExceptionDto(
            e.Id, e.CaseId, e.Type, e.Status, e.Reason,
            e.OwnerKind is { } kind && e.OwnerId is { } ownerId ? new OwnerDto(kind, ownerId) : null,
            e.RaisedAt, e.ResolvedAt)).ToList();
        return new PagedResult<CaseExceptionDto>(items, page, pageSize, total);
    }

    private static IQueryable<SummaryRow> Summaries(IQueryable<CoordinationCase> cases) =>
        cases.Select(c => new SummaryRow(
            c.Id, c.SourceSystem, c.PatientRef, c.ResultRef, c.Stage, c.OwnerKind, c.OwnerId,
            c.Exceptions.Count(e => e.Status == ExceptionStatus.Open), c.CreatedAt, c.UpdatedAt));

    private static CaseSummaryDto ToDto(SummaryRow r) => new(
        r.Id, r.SourceSystem, r.PatientRef, r.ResultRef, ToStageDto(r.Stage),
        r.OwnerKind is { } kind && r.OwnerId is { } id ? new OwnerDto(kind, id) : null,
        r.OpenExceptionCount, r.CreatedAt, r.UpdatedAt);

    private static StageDto ToStageDto(CaseStage stage) => new((int)stage, StageCatalog.Get(stage).Code);

    private sealed record SummaryRow(
        Guid Id, string SourceSystem, string PatientRef, string ResultRef, CaseStage Stage,
        OwnerKind? OwnerKind, string? OwnerId, int OpenExceptionCount, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
}

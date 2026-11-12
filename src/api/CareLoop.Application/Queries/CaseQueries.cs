using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Stages;

namespace CareLoop.Application.Queries;

public sealed record StageDto(int Number, string Code);

public sealed record OwnerDto(OwnerKind Kind, string Id);

public sealed record CaseSummaryDto(
    Guid Id,
    string SourceSystem,
    string PatientRef,
    string ResultRef,
    StageDto Stage,
    OwnerDto? Owner,
    int OpenExceptionCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record StageTransitionDto(StageDto? From, StageDto To, string ActorId, ActorKind ActorKind, DateTimeOffset OccurredAt);

public sealed record TimelineEntryDto(
    int Sequence,
    Guid EventId,
    string EventType,
    string SourceSystem,
    string SourceEventId,
    ActorKind ActorKind,
    string ActorId,
    string? ActorRole,
    EventDisposition Disposition,
    DateTimeOffset OccurredAt,
    DateTimeOffset ReceivedAt,
    StageTransitionDto? Transition);

public sealed record CaseExceptionDto(
    Guid Id,
    Guid CaseId,
    ExceptionType Type,
    ExceptionStatus Status,
    string Reason,
    OwnerDto? Owner,
    DateTimeOffset RaisedAt,
    DateTimeOffset? ResolvedAt);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record CaseListQuery(
    CaseStage? Stage = null,
    bool? HasOpenExceptions = null,
    OwnerKind? OwnerKind = null,
    string? OwnerId = null,
    int Page = 1,
    int PageSize = 25)
{
    public const int MaxPageSize = 100;

    public int NormalizedPage => Math.Max(1, Page);

    public int NormalizedPageSize => Math.Clamp(PageSize, 1, MaxPageSize);
}

public sealed record ExceptionListQuery(
    ExceptionStatus? Status = ExceptionStatus.Open, int Page = 1, int PageSize = 25)
{
    public int NormalizedPage => Math.Max(1, Page);

    public int NormalizedPageSize => Math.Clamp(PageSize, 1, CaseListQuery.MaxPageSize);
}

/// <summary>Read side. Implementations must not track entities or mutate state.</summary>
public interface ICaseQueries
{
    Task<PagedResult<CaseSummaryDto>> ListCasesAsync(CaseListQuery query, CancellationToken cancellationToken);

    Task<CaseSummaryDto?> GetCaseAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Returns null when the case does not exist.</summary>
    Task<IReadOnlyList<TimelineEntryDto>?> GetTimelineAsync(Guid caseId, CancellationToken cancellationToken);

    Task<PagedResult<CaseExceptionDto>> ListExceptionsAsync(ExceptionListQuery query, CancellationToken cancellationToken);
}

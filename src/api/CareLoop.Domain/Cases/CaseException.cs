namespace CareLoop.Domain.Cases;

public enum ExceptionType
{
    /// <summary>No accountable owner or queue could be assigned automatically.</summary>
    OwnerUnassigned,

    /// <summary>The source supplied no reviewed urgency; none is ever inferred.</summary>
    ReviewedUrgencyMissing,

    /// <summary>A source event conflicts with an existing case or event and needs human review.</summary>
    ConflictingSourceEvent,
}

public enum ExceptionStatus
{
    Open,
    Resolved,
}

/// <summary>Visible review work. An exception never advances or erases the case.</summary>
public sealed class CaseException
{
    private CaseException() { }

    public Guid Id { get; private set; }
    public Guid CaseId { get; private set; }
    public ExceptionType Type { get; private set; }
    public ExceptionStatus Status { get; private set; }
    public string Reason { get; private set; } = "";
    public OwnerKind? OwnerKind { get; private set; }
    public string? OwnerId { get; private set; }
    public Guid RaisedByEventId { get; private set; }
    public DateTimeOffset RaisedAt { get; private set; }
    public Guid? ResolvedByEventId { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    internal static CaseException Raise(
        Guid caseId, ExceptionType type, string reason, Guid eventId, DateTimeOffset at, CaseOwner? owner) => new()
        {
            Id = Guid.CreateVersion7(),
            CaseId = caseId,
            Type = type,
            Status = ExceptionStatus.Open,
            Reason = reason,
            OwnerKind = owner?.Kind,
            OwnerId = owner?.Id,
            RaisedByEventId = eventId,
            RaisedAt = at,
        };

    internal void Resolve(Guid eventId, DateTimeOffset at)
    {
        Status = ExceptionStatus.Resolved;
        ResolvedByEventId = eventId;
        ResolvedAt = at;
    }
}

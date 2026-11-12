using CareLoop.Domain.Cases;
using CareLoop.Domain.Stages;

namespace CareLoop.Application.Intake;

public enum IntakeStatus
{
    Accepted,

    /// <summary>The same source event was already processed; the original outcome is returned.</summary>
    Duplicate,

    Invalid,
    UnsupportedEventType,
    Forbidden,
    NotFound,

    /// <summary>A domain rule refused the event; nothing was recorded.</summary>
    Rejected,

    /// <summary>The event conflicts with existing state; it is recorded and review work is raised.</summary>
    HeldForReview,

    /// <summary>A concurrent writer won; the client may safely resend the same event.</summary>
    ConcurrentUpdate,
}

public sealed record IntakeResult(
    IntakeStatus Status,
    Guid? EventId = null,
    Guid? CaseId = null,
    CaseStage? Stage = null,
    string? Code = null,
    string? Message = null,
    IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static IntakeResult Accepted(Guid eventId, CoordinationCase @case) =>
        new(IntakeStatus.Accepted, eventId, @case.Id, @case.Stage);

    public static IntakeResult Duplicate(Guid eventId, Guid caseId, CaseStage? stage) =>
        new(IntakeStatus.Duplicate, eventId, caseId, stage);

    public static IntakeResult Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new(IntakeStatus.Invalid, Code: "validation_failed", Message: "The event envelope is invalid.", Errors: errors);
}

public enum HandlerOutcome
{
    Accepted,
    Invalid,
    NotFound,
    Rejected,
    HeldForReview,
}

/// <summary>What a handler decided. Only Accepted and HeldForReview are persisted.</summary>
public sealed record HandlerResult(
    HandlerOutcome Outcome,
    CoordinationCase? Case = null,
    string? Code = null,
    string? Message = null,
    IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static HandlerResult Accepted(CoordinationCase @case) => new(HandlerOutcome.Accepted, @case);

    public static HandlerResult Invalid(string field, string message) =>
        new(HandlerOutcome.Invalid, Code: "validation_failed", Message: "The event payload is invalid.",
            Errors: new Dictionary<string, string[]> { [field] = [message] });

    public static HandlerResult Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new(HandlerOutcome.Invalid, Code: "validation_failed", Message: "The event payload is invalid.", Errors: errors);

    public static HandlerResult NotFound(string code, string message) => new(HandlerOutcome.NotFound, Code: code, Message: message);

    public static HandlerResult Rejected(string code, string message) => new(HandlerOutcome.Rejected, Code: code, Message: message);

    public static HandlerResult HeldForReview(CoordinationCase @case, string code, string message) =>
        new(HandlerOutcome.HeldForReview, @case, code, message);

    public static HandlerResult FromTransitionError(TransitionError error) => Rejected(error.Code, error.Message);
}

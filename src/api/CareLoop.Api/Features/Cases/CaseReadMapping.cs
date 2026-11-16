using CareLoop.Api.Infrastructure.Auth;
using CareLoop.Contracts.Cases;
using CareLoop.Contracts.Common;
using CareLoop.Contracts.Exceptions;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Tracks;

namespace CareLoop.Api.Features.Cases;

/// <summary>Projects coordination state into staff reads, with coordinator demographics redacted.</summary>
internal static class CaseReadMapping
{
    /// <summary>Projects a case-list row.</summary>
    /// <param name="case">The no-tracking case.</param>
    /// <param name="actor">The reader.</param>
    /// <param name="pathways">The configured pathways.</param>
    /// <returns>The summary.</returns>
    public static CaseSummaryResponse Summary(Case @case, Actor actor, IPathwayProvider pathways) => new()
    {
        CaseId = @case.Id, PatientReference = @case.PatientReference,
        PatientDisplayName = actor.Role == DemoRoles.Coordinator ? null : @case.PatientDisplayName,
        PatientSecondaryIdentifier = actor.Role == DemoRoles.Coordinator ? null : @case.PatientSecondaryIdentifier,
        FacilityId = @case.FacilityId, PathwayId = @case.PathwayId, Stage = @case.Stage,
        DisplayLabel = CaseDisplayLabels.From(@case, pathways.Get(@case.PathwayId)), Urgency = @case.Urgency,
        Owner = Owner(@case.Owner), NextAction = @case.NextAction, DueAt = @case.DueAt,
        IsEscalated = @case.IsEscalated, Exceptions = Exceptions(@case.Exceptions),
        CreatedAt = @case.CreatedAt, UpdatedAt = @case.UpdatedAt, ClosureReason = @case.Closure?.Reason,
    };

    /// <summary>Projects full case coordination state.</summary>
    /// <param name="case">The no-tracking case.</param>
    /// <param name="actor">The reader.</param>
    /// <param name="pathways">The configured pathways.</param>
    /// <returns>The detail response.</returns>
    public static CaseDetailResponse Detail(Case @case, Actor actor, IPathwayProvider pathways) => new()
    {
        CaseId = @case.Id, SourceSystem = @case.SourceSystem, SourceResultId = @case.SourceResultId,
        PatientReference = @case.PatientReference,
        PatientDisplayName = actor.Role == DemoRoles.Coordinator ? null : @case.PatientDisplayName,
        PatientSecondaryIdentifier = actor.Role == DemoRoles.Coordinator ? null : @case.PatientSecondaryIdentifier,
        PathwayId = @case.PathwayId, FacilityId = @case.FacilityId, Stage = @case.Stage,
        DisplayLabel = CaseDisplayLabels.From(@case, pathways.Get(@case.PathwayId)), Urgency = @case.Urgency,
        Owner = Owner(@case.Owner), ProposedOwner = @case.PendingAssignment is { } p ? Owner(p.ProposedOwner) : null,
        NextAction = @case.NextAction, DueAt = @case.DueAt, IsEscalated = @case.IsEscalated,
        Exceptions = Exceptions(@case.Exceptions), OutcomeEvidenceReference = @case.OutcomeEvidenceReference,
        OutcomeEvidenceKind = @case.OutcomeEvidenceKind, Confidence = @case.Notification?.Confidence,
        ClosureReason = @case.Closure?.Reason, CreatedAt = @case.CreatedAt, UpdatedAt = @case.UpdatedAt,
        Escalations = @case.Escalations.Select(e => new EscalationResponse
        {
            EscalationId = e.Id, Level = e.Level, EscalatedTo = Owner(e.EscalatedTo), RaisedAt = e.RaisedAt,
            Reason = e.Reason, AcknowledgedBy = e.AcknowledgedBy?.Reference, AcknowledgedByRole = e.AcknowledgedBy?.Role,
            AcknowledgedAt = e.AcknowledgedAt, ResolvedAt = e.ResolvedAt,
        }).ToList(),
        Tracks =
        [
            Track(TrackKind.Notification, @case.Notification?.Status.ToString(), @case.Notification?.IsComplete() == true, @case.Notification?.UpdatedAt),
            Track(TrackKind.Consultation, @case.Consultation?.Status.ToString(), @case.Consultation?.IsComplete() == true, @case.Consultation?.UpdatedAt),
            Track(TrackKind.Referral, @case.Referral?.Status.ToString(), @case.Referral?.IsComplete() == true, @case.Referral?.UpdatedAt),
            Track(TrackKind.Appointment, @case.Appointment?.Status.ToString(), @case.Appointment?.IsComplete() == true, @case.Appointment?.UpdatedAt),
        ],
    };

    /// <summary>Projects an actionable exception row; a missing next action or deadline stays visible as null.</summary>
    /// <param name="case">The no-tracking case.</param>
    /// <param name="actor">The reader.</param>
    /// <param name="pathways">The configured pathways.</param>
    /// <param name="now">The read time.</param>
    /// <returns>The exception.</returns>
    public static ExceptionItemResponse Exception(Case @case, Actor actor, IPathwayProvider pathways, DateTimeOffset now)
    {
        var summary = Summary(@case, actor, pathways);
       var due = @case.DueAt;
        return new ExceptionItemResponse
        {
            CaseId = summary.CaseId, PatientReference = summary.PatientReference,
            PatientDisplayName = summary.PatientDisplayName, PatientSecondaryIdentifier = summary.PatientSecondaryIdentifier,
            FacilityId = summary.FacilityId, PathwayId = summary.PathwayId, DisplayLabel = summary.DisplayLabel,
            Stage = summary.Stage, Urgency = summary.Urgency, Owner = summary.Owner, Exceptions = summary.Exceptions,
           NextAction = summary.NextAction,
           DueAt = due, OverdueBy = due is { } dueAt && now > dueAt ? now - dueAt : null, IsEscalated = summary.IsEscalated,
            EscalationLevel = @case.Escalations.Where(e => e.IsOpen).Select(e => e.Level).DefaultIfEmpty().Max(),
        };
    }

    /// <summary>Projects an accountable staff owner.</summary>
    /// <param name="owner">The domain owner.</param>
    /// <returns>The owner contract.</returns>
    public static OwnerResponse Owner(CaseOwner owner) => new()
    {
        Reference = owner.Reference, Role = owner.Role, FacilityId = owner.FacilityId, IsQueue = owner.IsQueue,
    };

    /// <summary>Splits exception flags into stable names.</summary>
    /// <param name="flags">The unresolved flags.</param>
    /// <returns>Individual exception names.</returns>
    public static IReadOnlyList<string> Exceptions(CaseExceptions flags) =>
        Enum.GetValues<CaseExceptions>().Where(f => f != CaseExceptions.None && flags.HasFlag(f)).Select(f => f.ToString()).ToList();

    private static TrackStatusResponse Track(TrackKind kind, string? status, bool complete, DateTimeOffset? at) =>
        new() { Kind = kind, Status = status, IsComplete = complete, UpdatedAt = at };
}

using System.Text.Json;
using CareLoop.Api.Features.Events;
using CareLoop.Api.Infrastructure.Auth;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Contracts.Cases;
using CareLoop.Contracts.Common;
using CareLoop.Contracts.Exceptions;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Tracks;

namespace CareLoop.Api.Features.Cases;

/// <summary>
/// Projects coordination state into staff reads. The coordinator sees no one's identity: no name, no record number,
/// no patient reference and no hospital result identifier, only CareLoop's case number.
/// </summary>
internal static class CaseReadMapping
{
    /// <summary>Projects the compact fields shared with an exception row.</summary>
    /// <param name="case">The no-tracking case.</param>
    /// <param name="actor">The reader.</param>
    /// <param name="pathways">The configured pathways.</param>
    /// <returns>The summary.</returns>
    public static CaseSummaryResponse Summary(Case @case, Actor actor, IPathwayProvider pathways) => new()
    {
        CaseId = @case.Id, PatientReference = actor.Role == DemoRoles.Coordinator ? null : @case.PatientReference,
        PatientDisplayName = actor.Role == DemoRoles.Coordinator ? null : @case.PatientDisplayName,
        PatientSecondaryIdentifier = actor.Role == DemoRoles.Coordinator ? null : @case.PatientSecondaryIdentifier,
        FacilityId = @case.FacilityId, PathwayId = @case.PathwayId, Stage = @case.Stage,
        DisplayLabel = CaseDisplayLabels.From(@case, pathways.Get(@case.PathwayId)), Urgency = @case.Urgency,
        Owner = Owner(@case.Owner), NextAction = @case.NextAction, DueAt = @case.DueAt,
        IsEscalated = @case.IsEscalated, Exceptions = Exceptions(@case.Exceptions),
        CreatedAt = @case.CreatedAt, UpdatedAt = @case.UpdatedAt, ClosureReason = @case.Closure?.Reason,
    };

    /// <summary>Projects full case coordination state and the source-recorded patient language.</summary>
    /// <param name="case">The authorized case.</param>
    /// <param name="actor">The reader.</param>
    /// <param name="pathways">The configured pathways.</param>
    /// <param name="db">The source-event read context.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>The detail response, with patient language hidden from coordinators.</returns>
    public static async Task<CaseDetailResponse> DetailAsync(Case @case, Actor actor, IPathwayProvider pathways,
        CareLoopDbContext db, CancellationToken ct) =>
        (await DetailsAsync([@case], actor, pathways, db, ct))[0];

    /// <summary>Projects a page of full cases, loading patient languages together rather than once per row.</summary>
    /// <param name="cases">The authorized, ordered page.</param>
    /// <param name="actor">The reader.</param>
    /// <param name="pathways">The configured pathways.</param>
    /// <param name="db">The source-event read context.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>Full case responses in the supplied order.</returns>
    public static async Task<IReadOnlyList<CaseDetailResponse>> DetailsAsync(IReadOnlyList<Case> cases, Actor actor,
        IPathwayProvider pathways, CareLoopDbContext db, CancellationToken ct)
    {
        var payloads = await ScreeningResultReader.ReadPayloadsAsync(db,
            actor.Role == DemoRoles.Coordinator ? [] : cases.Select(c => c.Id).ToArray(), ct);
        return cases.Select(c => Detail(c, actor, pathways,
            payloads.TryGetValue(c.Id, out var payload) ? PatientLanguage(payload) : null)).ToList();
    }

    private static string? PatientLanguage(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.TryGetProperty("patientLanguage", out var language) ? language.GetString() : null;
    }

    /// <summary>Projects full case coordination state.</summary>
    /// <param name="case">The no-tracking case.</param>
    /// <param name="actor">The reader.</param>
    /// <param name="pathways">The configured pathways.</param>
    /// <param name="patientLanguage">The source-recorded language, or null when unavailable or redacted.</param>
    /// <returns>The detail response.</returns>
    private static CaseDetailResponse Detail(Case @case, Actor actor, IPathwayProvider pathways, string? patientLanguage) => new()
    {
        CaseId = @case.Id, SourceSystem = @case.SourceSystem,
        SourceResultId = actor.Role == DemoRoles.Coordinator ? null : @case.SourceResultId,
        PatientReference = actor.Role == DemoRoles.Coordinator ? null : @case.PatientReference,
        PatientDisplayName = actor.Role == DemoRoles.Coordinator ? null : @case.PatientDisplayName,
        PatientSecondaryIdentifier = actor.Role == DemoRoles.Coordinator ? null : @case.PatientSecondaryIdentifier,
        PatientLanguage = patientLanguage,
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

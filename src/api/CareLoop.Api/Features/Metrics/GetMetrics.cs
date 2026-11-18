using CareLoop.Api.Features.Cases;
using CareLoop.Api.Features.Common;
using CareLoop.Api.Infrastructure.Auth;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Contracts.Metrics;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Time;
using CareLoop.Domain.Tracks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CareLoop.Api.Features.Metrics;

/// <summary>Aggregates accountable coordination progress, not clinical effectiveness.</summary>
public static class GetMetrics
{
    /// <summary>Maps the metrics route.</summary>
    /// <param name="routes">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapGetMetrics(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/metrics", Handle).WithName("GetMetrics").WithTags("Metrics")
            .Produces<MetricsResponse>().ProducesProblem(400).ProducesProblem(401).ProducesProblem(403);
        return routes;
    }

    /// <summary>Computes current counts and actual applied-event history within an audited scope.</summary>
    /// <param name="facilityId">Optional facility; coordinators otherwise receive global aggregates.</param>
    /// <param name="reason">Reason for staff reading aggregates outside their facility.</param>
    /// <param name="context">The reader's identity.</param>
    /// <param name="db">The no-tracking read context.</param>
    /// <param name="pathways">The configured display-label and closure rules.</param>
    /// <param name="clock">The measurement clock.</param>
    /// <param name="loggerFactory">Records cases left out of a measure because their history is inconsistent.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>Counts, rates with denominators and elapsed clock-time medians, or an access refusal.</returns>
    /// <remarks>
    /// A case whose history cannot support a measure (a source timestamp earlier than the screening result, or a
    /// treatment plan without its deadline) is left out of that measure's numerator and denominator and logged,
    /// so one inconsistent case never stops the Impact screen for every reader.
    /// </remarks>
    public static async Task<IResult> Handle([FromQuery] string? facilityId, [FromQuery] string? reason,
        HttpContext context, CareLoopDbContext db, IPathwayProvider pathways, IClock clock, ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        if (ReadAccess.Identity(context, out var actor) is { } error) return error;
        if (ReadAccess.ValidateReason(reason) is { } invalid) return invalid;
        if (facilityId?.Length > 100 || facilityId is not null && string.IsNullOrWhiteSpace(facilityId))
            return Results.Problem("Invalid facility filter.", statusCode: 400);
        var queryText = ReadAccess.Query(("facilityId", facilityId ?? (actor.Role == DemoRoles.Coordinator ? null : actor.FacilityId)));
        if (queryText.Length > 500) return Results.Problem("The encoded query exceeds 500 characters.", statusCode: 400);
        var outside = actor.Role != DemoRoles.Coordinator && facilityId is not null && facilityId != actor.FacilityId;
        if (ReadAccess.CrossFacilityPermission(actor, facilityId, reason) is { } denied)
        {
            await ReadAccess.AuditAsync(db, actor, clock, "metrics-denied", null, outside, reason, queryText, ct);
            return denied;
        }

        var query = ReadAccess.FacilityScope(db.Cases.AsNoTracking(), actor, facilityId);
        var cases = await query.ToListAsync(ct);
        var auditRows = await (from audit in db.AuditEvents.AsNoTracking()
            join @case in query on audit.CaseId equals @case.Id
            where audit.Disposition == SourceEventDisposition.Applied
            select new { audit.CaseId, audit.Id, audit.EventType, audit.SourceTimestamp, audit.ReceivedTimestamp, audit.NewProjectionJson })
            .ToListAsync(ct);
        var history = auditRows.GroupBy(a => a.CaseId).ToDictionary(g => g.Key, g => g
            .Select(a => new HistoryEntry(a.EventType, a.SourceTimestamp, a.ReceivedTimestamp, CaseProjection.FromJson(a.NewProjectionJson)))
            .OrderBy(e => e.ReceivedAt).ThenBy(e => e.Projection.Version).ToList());
        var logger = loggerFactory.CreateLogger(typeof(GetMetrics));
        var response = Compute(cases, history, pathways, clock.UtcNow,
            (caseId, problem) => logger.LogWarning("Case {CaseId} left out of a metric: {Problem}", caseId, problem));
        await ReadAccess.AuditAsync(db, actor, clock, "metrics", null, outside, reason, queryText, ct);
        return Results.Ok(response);
    }

    private static MetricsResponse Compute(List<Case> cases, Dictionary<Guid, List<HistoryEntry>> history,
        IPathwayProvider pathways, DateTimeOffset now, Action<Guid, string> exclude)
    {
        var open = cases.Where(c => !c.IsClosed).ToList();
        var closed = cases.Where(c => c.IsClosed).ToList();
        var notified = cases.Where(c => c.Notification is not null).ToList();
        var acknowledged = notified.Where(c => c.Notification!.IsComplete()).ToList();
        var rated = notified.Where(c => c.Notification!.Confidence is not null).ToList();
        var referrals = cases.Where(c => c.Referral is not null).ToList();
        var appointmentsDue = cases.Where(c => c.Appointment?.ScheduledFor <= now).ToList();
        var steps = new Dictionary<string, List<TimeSpan>>
        {
            ["acknowledgement"] = [], ["consultation"] = [], ["appointment"] = [], ["careStarted"] = [], ["closure"] = [],
        };
        var labelsReached = Enum.GetValues<CaseDisplayLabel>().ToDictionary(l => l.ToString(), _ => 0);
        var deadlineEligible = 0;
        var onTime = 0;
        var treatmentPlans = 0;
        var treatmentOnTime = 0;
        foreach (var @case in cases)
        {
            var events = history.GetValueOrDefault(@case.Id) ?? [];
            var pathway = pathways.Get(@case.PathwayId);
            var start = events.FirstOrDefault(e => e.Type == CaseEventType.ScreeningResultReceived)?.At ?? @case.CreatedAt;
            AddDuration(steps["acknowledgement"], events.FirstOrDefault(e => e.Projection.Notification?.IsComplete == true)?.At, start, @case.Id, exclude);
            AddDuration(steps["consultation"], events.FirstOrDefault(e => e.Projection.Consultation?.Status == nameof(ConsultationStatus.OutcomeRecorded))?.At, start, @case.Id, exclude);
            AddDuration(steps["appointment"], events.FirstOrDefault(e => e.Projection.Appointment?.Status == nameof(AppointmentStatus.Attended))?.At, start, @case.Id, exclude);
            AddDuration(steps["careStarted"], events.FirstOrDefault(e => e.Type == CaseEventType.OutcomeRecorded
                && e.Projection.OutcomeEvidenceKind is "treatment-started" or "surveillance-started")?.At, start, @case.Id, exclude);
            AddDuration(steps["closure"], @case.Closure?.DecidedAt, start, @case.Id, exclude);
            var reached = events.SelectMany(e => Reached(Label(e.Projection, pathway), e.Projection.Stage,
                    e.Projection.Notification?.IsComplete == true, e.Projection.Consultation is not null,
                    e.Projection.Referral is not null || e.Projection.Appointment is not null))
                .Concat(Reached(CaseDisplayLabels.From(@case, pathway), @case.Stage, @case.Notification?.IsComplete() == true,
                    @case.Consultation is not null, @case.Referral is not null || @case.Appointment is not null))
                .Distinct();
            foreach (var label in reached) labelsReached[label.ToString()]++;

            var episodes = DeadlineEpisodes(events);
            var eligible = episodes.LastOrDefault(e => e.DueAt < now);
            if (eligible is not null)
            {
                deadlineEligible++;
                if (eligible.CompletedAt is { } completed && completed <= eligible.DueAt) onTime++;
            }

            var plan = events.FirstOrDefault(e => e.Type == CaseEventType.OutcomeRecorded && e.Projection.OutcomeEvidenceKind == "treatment-plan");
            if (plan is not null)
            {
                if (plan.Projection.NextAction != "start-treatment" || plan.Projection.DueAt is null)
                {
                    exclude(@case.Id, "its treatment-plan audit has no treatment deadline");
                    continue;
                }

                treatmentPlans++;
                var started = events.FirstOrDefault(e => e.Type == CaseEventType.OutcomeRecorded
                    && e.Projection.OutcomeEvidenceKind == "treatment-started" && e.At >= plan.At);
                if (started is not null && started.At <= plan.Projection.DueAt) treatmentOnTime++;
            }
        }

        var measures = new Dictionary<string, RateMeasureResponse>
        {
            ["acknowledgement"] = Rate(acknowledged.Count, notified.Count),
            ["closure"] = Rate(closed.Count, cases.Count),
            ["understanding"] = Rate(acknowledged.Count(c => c.Notification!.Status == NotificationStatus.Understood), acknowledged.Count),
            ["nextStepOnTime"] = Rate(onTime, deadlineEligible),
            ["referralAcceptedAndAttended"] = Rate(referrals.Count(c => c.Referral!.Status == ReferralStatus.Accepted && c.Appointment?.Status == AppointmentStatus.Attended), referrals.Count),
            ["appointmentsAttended"] = Rate(appointmentsDue.Count(c => c.Appointment!.Status == AppointmentStatus.Attended), appointmentsDue.Count),
            ["treatmentStartedOnTime"] = Rate(treatmentOnTime, treatmentPlans),
            ["signedEvidence"] = Rate(closed.Count(c => !string.IsNullOrWhiteSpace(c.Closure!.EvidenceReference)), closed.Count),
        };
        return new MetricsResponse
        {
            GeneratedAt = now, TotalCases = cases.Count, OpenCases = open.Count, ClosedCases = closed.Count,
            CasesByStage = Enum.GetValues<CaseStage>().ToDictionary(s => s.ToString(), s => open.Count(c => c.Stage == s)),
            CasesByException = Enum.GetValues<CaseExceptions>().Where(e => e != CaseExceptions.None)
                .ToDictionary(e => e.ToString(), e => open.Count(c => c.Exceptions.HasFlag(e))),
            OverdueCases = open.Count(c => c.DueAt < now),
            EscalatedCases = open.Count(c => c.IsEscalated),
            UnassignedCases = open.Count(c => c.Exceptions.HasFlag(CaseExceptions.Unassigned)),
            AcknowledgementRate = measures["acknowledgement"].Rate, ClosureRate = measures["closure"].Rate,
            MedianTimeToClosure = Median(steps["closure"]), Measures = measures,
            TimeToStep = steps.ToDictionary(p => p.Key, p => new DurationMeasureResponse { Denominator = p.Value.Count, Median = Median(p.Value) }),
            CasesByDisplayLabel = Enum.GetValues<CaseDisplayLabel>()
                .ToDictionary(l => l.ToString(), l => cases.Count(c => CaseDisplayLabels.From(c, pathways.Get(c.PathwayId)) == l)),
            CasesReachingDisplayLabel = labelsReached.ToDictionary(p => p.Key, p => Rate(p.Value, cases.Count)),
            ClosuresByReason = Enum.GetValues<ClosureReason>().ToDictionary(r => r.ToString(), r => closed.Count(c => c.Closure!.Reason == r)),
            ConfidenceRate = Rate(rated.Count, notified.Count).Rate, ConfidenceDenominator = notified.Count,
            ConfidenceResponses = rated.Count, AverageConfidence = rated.Count == 0 ? null : rated.Average(c => c.Notification!.Confidence!.Value),
        };
    }

    private static CaseDisplayLabel Label(CaseProjection p, PathwayDefinition pathway) =>
        CaseDisplayLabels.From(p.Stage, p.NextAction is not null,
            p.Notification is null ? null : Enum.Parse<NotificationStatus>(p.Notification.Status),
            p.Consultation is not null, p.Referral is not null, p.Appointment is not null,
            pathway.Tracks.Where(t => t.RequiredForClosure).All(t => t.Kind switch
            {
                TrackKind.Notification => p.Notification?.IsComplete == true,
                TrackKind.Consultation => p.Consultation?.IsComplete == true,
                TrackKind.Referral => p.Referral?.IsComplete == true,
                TrackKind.Appointment => p.Appointment?.IsComplete == true,
                _ => throw new InvalidOperationException($"Unknown required track {t.Kind}."),
            }));

    /// <summary>
    /// The display labels a case had reached at one point in its history: the one it was at, every step before it that
    /// no case can skip, and each optional step whose event had arrived.
    /// </summary>
    /// <remarks>
    /// One event can pass several labels. The screening result receives the result, opens the case and moves it to
    /// review at once, and an outcome that arrives when every required track is complete goes straight to closure
    /// review. Those steps were still reached. The optional steps (the patient acknowledging, a consultation, a referral
    /// or appointment, and closure review, which a closure by documented exception can bypass) count only when they
    /// happened, even if a further step hid them from the label.
    /// </remarks>
    private static IEnumerable<CaseDisplayLabel> Reached(CaseDisplayLabel label, CaseStage stage, bool acknowledged,
        bool consultation, bool referralOrAppointment)
    {
        yield return label;
        yield return CaseDisplayLabel.ResultReceived;
        if (stage is not CaseStage.Received)
        {
            yield return CaseDisplayLabel.CaseOpened;
            yield return CaseDisplayLabel.OwnerAssigned;
        }

        if (stage is CaseStage.FollowUpActive or CaseStage.ClosureReview or CaseStage.Closed) yield return CaseDisplayLabel.PatientNotified;
        if (acknowledged) yield return CaseDisplayLabel.PatientAcknowledged;
        if (consultation) yield return CaseDisplayLabel.Consultation;
        if (referralOrAppointment) yield return CaseDisplayLabel.FollowUp;
        if (stage is CaseStage.ClosureReview or CaseStage.Closed) yield return CaseDisplayLabel.OutcomeRecorded;
    }

    private static List<DeadlineEpisode> DeadlineEpisodes(List<HistoryEntry> history)
    {
        var episodes = new List<DeadlineEpisode>();
        foreach (var entry in history)
        {
            var projection = entry.Projection;
            var current = episodes.LastOrDefault();
            if (current is not null && current.CompletedAt is null
                && (projection.DueAt != current.DueAt || projection.NextAction != current.Action || projection.Stage == CaseStage.Closed))
                current.CompletedAt = entry.At;
            if (projection.DueAt is { } due && projection.NextAction is { } action
                && (current is null || current.DueAt != due || current.Action != action))
                episodes.Add(new DeadlineEpisode(action, due));
        }
        return episodes;
    }

    private static void AddDuration(List<TimeSpan> values, DateTimeOffset? at, DateTimeOffset start, Guid caseId,
        Action<Guid, string> exclude)
    {
        if (at is null) return;
        if (at < start)
        {
            exclude(caseId, "a measured step precedes its source screening result");
            return;
        }

        values.Add(at.Value - start);
    }

    private static TimeSpan? Median(List<TimeSpan> values)
    {
        if (values.Count == 0) return null;
        var ordered = values.Order().ToList();
        var middle = ordered.Count / 2;
        return ordered.Count % 2 == 1 ? ordered[middle]
            : TimeSpan.FromTicks(ordered[middle - 1].Ticks + (ordered[middle].Ticks - ordered[middle - 1].Ticks) / 2);
    }

    private static RateMeasureResponse Rate(int numerator, int denominator) => new() { Numerator = numerator, Denominator = denominator };
    private sealed record HistoryEntry(CaseEventType Type, DateTimeOffset At, DateTimeOffset ReceivedAt, CaseProjection Projection);
    private sealed class DeadlineEpisode(string action, DateTimeOffset dueAt)
    {
        public string Action { get; } = action;
        public DateTimeOffset DueAt { get; } = dueAt;
        public DateTimeOffset? CompletedAt { get; set; }
    }
}

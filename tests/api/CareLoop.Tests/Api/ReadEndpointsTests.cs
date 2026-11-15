using System.Text.Json;
using System.Text.Json.Serialization;
using CareLoop.Api.Features.Cases;
using CareLoop.Api.Features.Events;
using CareLoop.Api.Features.Exceptions;
using CareLoop.Api.Features.Metrics;
using CareLoop.Api.Features.Programmes;
using CareLoop.Api.Infrastructure;
using CareLoop.Api.Infrastructure.Auth;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Api.Infrastructure.Persistence.Outbox;
using CareLoop.Contracts.Cases;
using CareLoop.Contracts.Events;
using CareLoop.Contracts.Events.Payloads;
using CareLoop.Contracts.Exceptions;
using CareLoop.Contracts.Metrics;
using CareLoop.Contracts.Programmes;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Time;
using CareLoop.Domain.Tracks;
using CareLoop.Tests.Domain;
using CareLoop.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyInjection;

namespace CareLoop.Tests.Api;

/// <summary>Audited reads use the real SQLite store and enforce identities, scoping, redaction and measured history.</summary>
public sealed class ReadEndpointsTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly FakeClock _clock = new();
    private readonly ServiceProvider _services;
    private readonly IPathwayProvider _pathways = DomainTestData.Provider();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public ReadEndpointsTests()
    {
        _connection.Open();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IClock>(_clock);
        services.AddSingleton(_pathways);
        services.AddDbContext<CareLoopDbContext>(o => o.UseSqlite(_connection).ReplaceService<IModelCustomizer, UtcModelCustomizer>());
        services.AddSingleton(new OutboxSubscriberRegistry(() => ["FhirPublisher", "NotificationRouter"]));
        services.AddScoped<ICaseRepository, EfCaseRepository>();
        services.AddIntake();
        _services = services.BuildServiceProvider();
        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<CareLoopDbContext>().Database.EnsureCreated();
    }

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task List_cases_filters_by_stage_owner_and_escalation()
    {
        var first = await SeedAsync();
        _clock.Advance(TimeSpan.FromHours(1));
        var second = NewCase("SYN-RESULT-2", "SYN-FAC-A");
        second.AssignOwner(TestData.Clinician("SYN-STAFF-2"), _clock.UtcNow);
        second.MoveTo(CaseStage.FollowUpActive, _clock.UtcNow);
        second.RecordReview("PLACEHOLDER-URGENCY-A", DomainTestData.ClinicianActor(), _clock.UtcNow);
        second.Escalate(Escalation(1));
        await SeedAsync(second);
        var filtered = Rows<CaseSummaryResponse>(await ListAsync(stage: CaseStage.FollowUpActive, owner: second.Owner.Reference, escalated: true));
        Assert.Equal(second.Id, Assert.Single(filtered).CaseId);
        Assert.Equal(first.Id, Assert.Single(Rows<CaseSummaryResponse>(await ListAsync(escalated: false))).CaseId);
        Assert.Equal(second.Id, Rows<CaseSummaryResponse>(await ListAsync(take: 1))[0].CaseId);
        Assert.Equal(first.Id, Rows<CaseSummaryResponse>(await ListAsync(skip: 1, take: 1))[0].CaseId);
    }

    [Theory]
    [InlineData(-1, 50)]
    [InlineData(0, 0)]
    [InlineData(0, 201)]
    public async Task Invalid_paging_is_refused(int skip, int take) =>
        Assert.Equal(400, Status(await ListAsync(skip: skip, take: take)));

    [Fact]
    public async Task Get_case_returns_404_for_an_unknown_case() =>
        Assert.Equal(404, Status(await GetAsync(Guid.NewGuid())));

    [Fact]
    public async Task Get_case_shows_owner_and_pending_proposed_owner()
    {
        var @case = DomainTestData.InFollowUp(_clock);
        @case.ProposeOwner(new Assignment
        {
            ProposedOwner = TestData.Clinician("SYN-STAFF-2"), ProposedBy = DomainTestData.ClinicianActor(),
            ProposedAt = _clock.UtcNow, RespondBy = _clock.UtcNow.AddHours(4), Status = AssignmentStatus.Proposed,
        });
        await SeedAsync(@case);
        var response = Value<CaseDetailResponse>(await GetAsync(@case.Id));
        Assert.Equal(@case.Owner.Reference, response.Owner.Reference);
        Assert.Equal("SYN-STAFF-2", response.ProposedOwner!.Reference);
        Assert.Equal(CaseDisplayLabels.From(@case, _pathways.Get(@case.PathwayId)), response.DisplayLabel);
        Assert.Equal(4, response.Tracks.Count);
        Assert.False(response.Tracks.Single(t => t.Kind == TrackKind.Notification).IsComplete);
    }

    [Fact]
    public async Task Timeline_is_ordered_oldest_first_and_includes_rejected_attempts()
    {
        var @case = await SeedAsync();
        _clock.Advance(TimeSpan.FromHours(1));
        await SaveChangeAsync(@case.Id, c => c.FlagException(CaseExceptions.Overdue, _clock.UtcNow), CaseEventType.DeadlineElapsed, "careloop");
        _clock.Advance(TimeSpan.FromHours(1));
        await SaveChangeAsync(@case.Id, _ => { }, CaseEventType.ClosureDecided, "SYN-HIS", SourceEventDisposition.Rejected);
        var timeline = Rows<TimelineEntryResponse>(await TimelineAsync(@case.Id));
        Assert.Equal(3, timeline.Count);
        Assert.Equal(timeline.Select(e => e.ReceivedTimestamp).Order(), timeline.Select(e => e.ReceivedTimestamp));
        Assert.Equal(SourceEventDisposition.Rejected, timeline[2].Disposition);
        Assert.Equal("careloop", timeline[1].SourceSystem);
        Assert.Equal(CaseStage.ReviewPending, timeline[1].PriorStage);
        Assert.Equal("SYN-HIS", timeline[2].SourceSystem);
    }

    [Fact]
    public async Task Exceptions_lists_only_cases_with_an_unresolved_exception()
    {
        await SeedAsync();
        var flagged = NewCase("SYN-RESULT-2", "SYN-FAC-A");
        flagged.FlagException(CaseExceptions.Rejected, _clock.UtcNow);
        await SeedAsync(flagged);
        var closed = ClosedCase("SYN-CLOSED");
        await SeedAsync(closed);
        Assert.Equal(flagged.Id, Assert.Single(Rows<ExceptionItemResponse>(await ExceptionsAsync())).CaseId);
    }

    [Fact]
    public async Task Exception_row_shows_next_action_owner_and_deadline()
    {
        var @case = DomainTestData.InReviewPending(_clock);
        @case.FlagException(CaseExceptions.Overdue | CaseExceptions.Unassigned, _clock.UtcNow);
        @case.Escalate(Escalation(1));
        await SeedAsync(@case);
        _clock.Set(@case.DueAt!.Value.AddHours(2));
        var row = Assert.Single(Rows<ExceptionItemResponse>(await ExceptionsAsync()));
        Assert.Equal(@case.NextAction, row.NextAction);
        Assert.Equal(@case.Owner.Reference, row.Owner.Reference);
        Assert.Equal(@case.DueAt, row.DueAt);
        Assert.Equal(TimeSpan.FromHours(2), row.OverdueBy);
        Assert.Equal(1, row.EscalationLevel);
        Assert.Equal(["Overdue", "Unassigned"], row.Exceptions.Order());
    }

    [Fact]
    public async Task Metrics_counts_cases_by_stage_and_exception()
    {
        var @case = await SeedAsync();
        await SaveChangeAsync(@case.Id, c => { c.FlagException(CaseExceptions.Overdue, _clock.UtcNow); c.Escalate(Escalation(1)); });
        await SeedAsync(ClosedCase("SYN-CLOSED"));
        _clock.Advance(TimeSpan.FromDays(2));
        var metrics = Value<MetricsResponse>(await MetricsAsync());
        Assert.Equal(2, metrics.TotalCases);
        Assert.Equal(1, metrics.OpenCases);
        Assert.Equal(1, metrics.ClosedCases);
        Assert.Equal(1, metrics.CasesByStage[nameof(CaseStage.ReviewPending)]);
        Assert.Equal(1, metrics.CasesByException[nameof(CaseExceptions.Overdue)]);
        Assert.Equal(1, metrics.OverdueCases);
        Assert.Equal(1, metrics.EscalatedCases);
        Assert.Equal(0.5, metrics.ClosureRate);
        Assert.Equal(_clock.UtcNow, metrics.GeneratedAt);
    }

    [Fact]
    public async Task Empty_metrics_return_null_rates_and_medians_not_zero_success()
    {
        var metrics = Value<MetricsResponse>(await MetricsAsync());
        Assert.Equal(0, metrics.TotalCases);
        Assert.Null(metrics.AcknowledgementRate);
        Assert.Null(metrics.ClosureRate);
        Assert.Null(metrics.ConfidenceRate);
        Assert.Null(metrics.AverageConfidence);
        Assert.All(metrics.Measures.Values, m => { Assert.Equal(0, m.Denominator); Assert.Null(m.Rate); });
        Assert.All(metrics.TimeToStep.Values, m => { Assert.Equal(0, m.Denominator); Assert.Null(m.Median); });
    }

    [Fact]
    public async Task Responses_have_no_simulation_flags_or_labels()
    {
        var @case = await SeedAsync();
        foreach (var result in new[] { await ListAsync(), await GetAsync(@case.Id), await TimelineAsync(@case.Id), await MetricsAsync() })
        {
            var json = JsonSerializer.Serialize(((IValueHttpResult)result).Value, JsonOptions);
            Assert.DoesNotContain("simulated", json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("clinician")]
    [InlineData("nurse-navigator")]
    [InlineData("receiving-clinician")]
    [InlineData("lab")]
    [InlineData("facility-lead")]
    public async Task Staff_can_read_their_facility_and_receive_demographics(string role)
    {
        var @case = await SeedAsync(DomainTestData.InReviewPending(_clock));
        await SaveChangeAsync(@case.Id, c => { c.FlagException(CaseExceptions.Rejected, _clock.UtcNow); });
        var context = Identity(role);
        Assert.Equal(200, Status(await GetAsync(@case.Id, context)));
        Assert.Equal(200, Status(await ListAsync(context: context)));
    }

    [Fact]
    public async Task Cross_facility_reads_require_a_reason_and_are_audited_once()
    {
        var foreign = await SeedAsync(NewCase("SYN-FOREIGN", "SYN-FAC-B", name: "SYNTHETIC Patient B"));
        Assert.Empty(Rows<CaseSummaryResponse>(await ListAsync()));
        Assert.Equal(403, Status(await GetAsync(foreign.Id)));
        Assert.Equal(403, Status(await TimelineAsync(foreign.Id)));
        Assert.Equal(403, Status(await ListAsync(facility: "SYN-FAC-B")));
        var response = Value<CaseDetailResponse>(await GetAsync(foreign.Id, reason: "SYN covering the other facility"));
        Assert.Equal("SYNTHETIC Patient B", response.PatientDisplayName);
        Assert.Equal(200, Status(await TimelineAsync(foreign.Id, reason: "SYN covering the other facility")));
        var rows = Rows<CaseSummaryResponse>(await ListAsync(facility: "SYN-FAC-B", reason: "SYN covering the other facility"));
        Assert.Single(rows);
        using var scope = _services.CreateScope();
        var reads = await Db(scope).CaseReads.ToListAsync();
        Assert.Equal(7, reads.Count);
        Assert.Equal(3, reads.Count(r => r.View.EndsWith("-denied", StringComparison.Ordinal)));
        Assert.All(reads.Where(r => r.Reason is not null), r => Assert.True(r.OutsideTeam));
        Assert.All(reads, r => Assert.Equal("SYN-STAFF-1", r.ActorReference));
    }

    [Fact]
    public async Task Coordinator_reads_only_escalated_cases_with_demographics_redacted_but_global_totals()
    {
        var normal = await SeedAsync();
        var escalated = await SeedAsync(NewCase("SYN-ESC", "SYN-FAC-B", name: "SYNTHETIC Patient B"));
        await SaveChangeAsync(escalated.Id, c => c.Escalate(Escalation(2, "coordinator") with
        {
            EscalatedTo = TestData.FacilityLeadQueue() with { FacilityId = "SYN-FAC-B", Role = "coordinator" },
        }));
        var coordinator = Identity("coordinator", facility: null);
        Assert.Equal(403, Status(await GetAsync(normal.Id, coordinator)));
        var summary = Assert.Single(Rows<CaseSummaryResponse>(await ListAsync(context: coordinator)));
        Assert.Equal(escalated.Id, summary.CaseId);
        Assert.Null(summary.PatientDisplayName);
        Assert.Null(summary.PatientSecondaryIdentifier);
        var detail = Value<CaseDetailResponse>(await GetAsync(escalated.Id, coordinator));
        Assert.Null(detail.PatientDisplayName);
        Assert.Null(detail.PatientSecondaryIdentifier);
        Assert.Equal(2, Value<MetricsResponse>(await MetricsAsync(coordinator)).TotalCases);
    }

    [Theory]
    [InlineData("clinician", 200)]
    [InlineData("receiving-clinician", 200)]
    [InlineData("nurse-navigator", 403)]
    [InlineData("lab", 403)]
    [InlineData("facility-lead", 403)]
    [InlineData("coordinator", 403)]
    public async Task Patient_search_is_limited_to_clinician_roles_and_audited(string role, int status)
    {
        await SeedAsync();
        Assert.Equal(status, Status(await ListAsync(patient: "SYN-PT-0001", context: Identity(role))));
        using var scope = _services.CreateScope();
        var read = Assert.Single(await Db(scope).CaseReads.ToListAsync());
        Assert.Contains("patientReference=SYN-PT-0001", read.Query);
    }

    [Fact]
    public async Task Missing_or_invalid_identity_is_refused()
    {
        Assert.Equal(401, Status(await ListAsync(context: new DefaultHttpContext())));
        Assert.Equal(403, Status(await ListAsync(context: Identity("system"))));
        Assert.Equal(400, Status(await ListAsync(context: Identity("clinician", facility: null))));
    }

    [Fact]
    public async Task Read_slices_do_not_track_cases_or_change_the_case_projection()
    {
        var @case = await SeedAsync();
        using var scope = _services.CreateScope();
        var db = Db(scope);
        await GetCase.Handle(@case.Id, null, Identity(), db, _pathways, _clock, CancellationToken.None);
        Assert.Empty(db.ChangeTracker.Entries<Case>());
        Assert.Equal(@case.Version, (await db.Cases.AsNoTracking().SingleAsync()).Version);
        Assert.Single(await db.CaseReads.ToListAsync());
        Assert.Single(await db.SourceEvents.ToListAsync());
    }

    [Fact]
    public void Programmes_show_configured_citations_evidence_and_change_history()
    {
        var programme = Assert.Single(Rows<ProgrammeResponse>(GetProgrammes.List(Identity(), _pathways)));
        Assert.Equal(DomainTestData.Pathway.Version, programme.Version);
        Assert.NotEmpty(programme.ChangeLog);
        Assert.All(programme.Deadlines, d => { Assert.NotNull(d.OwnerRole); Assert.NotNull(d.Citation); Assert.False(d.ClinicallyApproved); });
        Assert.NotEmpty(programme.EvidenceKinds);
        Assert.Equal(404, Status(GetProgrammes.Get("SYN-unknown", Identity(), _pathways)));
        Assert.Equal(200, Status(GetProgrammes.Get(programme.PathwayId, Identity(), _pathways)));
    }

    [Fact]
    public async Task Acknowledgement_records_the_specific_escalation_without_changing_deadline_or_owner()
    {
        var @case = await SeedAsync();
        await SaveChangeAsync(@case.Id, c => { c.FlagException(CaseExceptions.Overdue, _clock.UtcNow); c.Escalate(Escalation(1)); });
        var before = await ReadCaseAsync(@case.Id);
        var id = before.Escalations.Single().Id;
        Assert.Equal(403, Status(await AcknowledgeAsync(@case.Id, id, Identity("clinician"))));
        var result = Value<CaseDetailResponse>(await AcknowledgeAsync(@case.Id, id, Identity("facility-lead")));
        Assert.Equal(before.Owner.Reference, result.Owner.Reference);
        Assert.Equal(before.DueAt, result.DueAt);
        Assert.Contains("Overdue", result.Exceptions);
        Assert.Equal("facility-lead", result.Escalations.Single().AcknowledgedByRole);
        Assert.Equal(409, Status(await AcknowledgeAsync(@case.Id, id, Identity("facility-lead"))));
        Assert.Equal(409, Status(await AcknowledgeAsync(@case.Id, Guid.NewGuid(), Identity("facility-lead"))));
        using var scope = _services.CreateScope();
        var audit = await Db(scope).AuditEvents.Where(a => a.EventType == CaseEventType.EscalationAcknowledged).ToListAsync();
        Assert.Equal(4, audit.Count);
        Assert.Equal(1, audit.Count(a => a.Disposition == SourceEventDisposition.Applied));
        Assert.Equal(3, audit.Count(a => a.Disposition == SourceEventDisposition.Rejected));
    }

    [Fact]
    public async Task Historical_measures_use_actual_events_and_treatment_plan_deadlines()
    {
        var @case = await IngestAsync(Envelopes.ScreeningResult(_clock.UtcNow, namedOwner: Envelopes.Clinician()));
        _clock.Advance(TimeSpan.FromHours(1));
        await IngestAsync(Envelopes.ClinicalReview(_clock.UtcNow));
        _clock.Advance(TimeSpan.FromHours(1));
        await IngestAsync(Envelopes.Notification(_clock.UtcNow, NotificationUpdateKind.Acknowledged));
        await IngestAsync(Envelopes.Notification(_clock.UtcNow, NotificationUpdateKind.Understood));
        await IngestAsync(Envelopes.Notification(_clock.UtcNow, NotificationUpdateKind.ConfidenceRated, confidence: 4));
        _clock.Advance(TimeSpan.FromHours(1));
        await IngestAsync(Envelopes.Referral(_clock.UtcNow, ReferralStatus.Accepted));
        await IngestAsync(Envelopes.Appointment(_clock.UtcNow, AppointmentStatus.Attended, _clock.UtcNow));
        await IngestAsync(Envelopes.Outcome(_clock.UtcNow, evidenceKind: "treatment-plan"));
        _clock.Advance(TimeSpan.FromHours(4));
        await IngestAsync(Envelopes.Outcome(_clock.UtcNow, evidenceKind: "treatment-started"));
        _clock.Advance(TimeSpan.FromHours(1));
        await IngestAsync(Envelopes.Closure(_clock.UtcNow));
        _clock.Advance(TimeSpan.FromDays(2));
        var metrics = Value<MetricsResponse>(await MetricsAsync());
        Assert.Equal(1, metrics.Measures["treatmentStartedOnTime"].Numerator);
        Assert.Equal(1, metrics.Measures["treatmentStartedOnTime"].Denominator);
        Assert.Equal(1, metrics.Measures["referralAcceptedAndAttended"].Numerator);
        Assert.Equal(1, metrics.Measures["understanding"].Rate);
        Assert.Equal(1, metrics.ConfidenceRate);
        Assert.Equal(4, metrics.AverageConfidence);
        Assert.Equal(TimeSpan.FromHours(2), metrics.TimeToStep["acknowledgement"].Median);
        Assert.Equal(TimeSpan.FromHours(7), metrics.TimeToStep["careStarted"].Median);
        Assert.Equal(1, metrics.ClosuresByReason["Treated"]);
        Assert.Equal(1, metrics.CasesReachingDisplayLabel["PatientAcknowledged"].Numerator);
        Assert.Equal(0, metrics.CasesReachingDisplayLabel["Consultation"].Numerator);
        Assert.Equal(1, metrics.CasesByDisplayLabel["Closed"]);
        Assert.Equal(@case.Id, Assert.Single(Rows<CaseSummaryResponse>(await ListAsync())).CaseId);
    }

    [Fact]
    public async Task Late_treatment_is_not_counted_on_time_and_rejected_outcomes_do_not_count()
    {
        await IngestAsync(Envelopes.ScreeningResult(_clock.UtcNow, namedOwner: Envelopes.Clinician()));
        await IngestAsync(Envelopes.ClinicalReview(_clock.UtcNow));
        await IngestAsync(Envelopes.Outcome(_clock.UtcNow, evidenceKind: "treatment-plan"));
        var plan = await ReadCaseAsync((await CasesAsync()).Single().Id);
        _clock.Set(plan.DueAt!.Value.AddTicks(1));
        await IngestAsync(Envelopes.Outcome(_clock.UtcNow, evidenceKind: "treatment-started"));
        var metrics = Value<MetricsResponse>(await MetricsAsync());
        Assert.Equal(1, metrics.Measures["treatmentStartedOnTime"].Denominator);
        Assert.Equal(0, metrics.Measures["treatmentStartedOnTime"].Numerator);
        Assert.Equal(0, metrics.Measures["nextStepOnTime"].Numerator);
    }

    private static DefaultHttpContext Identity(string role = "clinician", string? facility = "SYN-FAC-A")
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[DemoRoles.UserHeader] = "SYN-STAFF-1";
        context.Request.Headers[DemoRoles.RoleHeader] = role;
        if (facility is not null) context.Request.Headers[DemoRoles.FacilityHeader] = facility;
        return context;
    }

    private async Task<IResult> ListAsync(CaseStage? stage = null, string? owner = null, bool? escalated = null,
        int? skip = null, int? take = null, string? facility = null, string? patient = null, string? reason = null, HttpContext? context = null)
    {
        using var scope = _services.CreateScope();
        return await ListCases.Handle(stage, owner, escalated, skip, take, facility, patient, reason,
            context ?? Identity(), Db(scope), _pathways, _clock, CancellationToken.None);
    }

    private async Task<IResult> GetAsync(Guid id, HttpContext? context = null, string? reason = null)
    {
        using var scope = _services.CreateScope();
        return await GetCase.Handle(id, reason, context ?? Identity(), Db(scope), _pathways, _clock, CancellationToken.None);
    }

    private async Task<IResult> TimelineAsync(Guid id, HttpContext? context = null, string? reason = null)
    {
        using var scope = _services.CreateScope();
        return await GetTimeline.Handle(id, reason, context ?? Identity(), Db(scope), _clock, CancellationToken.None);
    }

    private async Task<IResult> ExceptionsAsync()
    {
        using var scope = _services.CreateScope();
        return await GetExceptions.Handle(null, null, null, Identity(), Db(scope), _pathways, _clock, CancellationToken.None);
    }

    private async Task<IResult> MetricsAsync(HttpContext? context = null)
    {
        using var scope = _services.CreateScope();
        return await GetMetrics.Handle(null, null, context ?? Identity(), Db(scope), _pathways, _clock, CancellationToken.None);
    }

    private async Task<IResult> AcknowledgeAsync(Guid caseId, Guid escalationId, HttpContext context)
    {
        using var scope = _services.CreateScope();
        return await AcknowledgeEscalation.Handle(caseId, escalationId, null, context,
            scope.ServiceProvider.GetRequiredService<ICaseRepository>(), _pathways, _clock, CancellationToken.None);
    }

    private async Task<Case> SeedAsync(Case? @case = null)
    {
        @case ??= DomainTestData.InReviewPending(_clock);
        @case.ClearDomainEvents();
        using var scope = _services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ICaseRepository>().SaveAsync(@case,
            TestData.AppliedEvent(@case, Guid.NewGuid().ToString("N"), _clock.UtcNow), CancellationToken.None);
        return @case;
    }

    private async Task SaveChangeAsync(Guid id, Action<Case> change, CaseEventType type = CaseEventType.ClinicalReviewCompleted,
        string source = "SYN-HIS", SourceEventDisposition disposition = SourceEventDisposition.Applied)
    {
        using var scope = _services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ICaseRepository>();
        var @case = (await repo.GetAsync(id, CancellationToken.None))!;
        change(@case);
        await repo.SaveAsync(@case, TestData.AppliedEvent(@case, Guid.NewGuid().ToString("N"), _clock.UtcNow, type)
            with { SourceSystem = source, Disposition = disposition, DispositionReason = "SYN test event" }, CancellationToken.None);
    }

    private async Task<Case> IngestAsync(EventEnvelope envelope)
    {
        using var scope = _services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<EventIngestor>().IngestAsync(envelope, CancellationToken.None);
        Assert.Equal(IngestOutcome.Applied, result.Outcome);
        return (await scope.ServiceProvider.GetRequiredService<ICaseRepository>().GetAsync(result.CaseId!.Value, CancellationToken.None))!;
    }

    private async Task<Case> ReadCaseAsync(Guid id)
    {
        using var scope = _services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<ICaseRepository>().GetAsync(id, CancellationToken.None))!;
    }

    private async Task<List<Case>> CasesAsync()
    {
        using var scope = _services.CreateScope();
        return await Db(scope).Cases.AsNoTracking().ToListAsync();
    }

    private Case NewCase(string result, string facility, string? name = null)
    {
        var @case = new Case(Guid.NewGuid(), "SYN-HIS", result, "SYN-PT-0001", "breast-screening", facility,
            TestData.Clinician() with { FacilityId = facility }, _clock.UtcNow, name, "SYN-MRN-1");
        @case.MoveTo(CaseStage.ReviewPending, _clock.UtcNow);
        @case.SetNextAction("review-result", _clock.UtcNow.AddDays(1), _clock.UtcNow);
        return @case;
    }

    private Case ClosedCase(string result)
    {
        var @case = NewCase(result, "SYN-FAC-A");
        @case.MoveTo(CaseStage.FollowUpActive, _clock.UtcNow);
        @case.BeginNotification("result-notification", _clock.UtcNow).Advance(NotificationStatus.Acknowledged, _clock.UtcNow, "SYN-COMM");
        @case.BeginAppointment("SYN-APPT", _clock.UtcNow, _clock.UtcNow).Advance(AppointmentStatus.Attended, _clock.UtcNow, "SYN-APPT");
        @case.RecordOutcomeEvidence("SYN-EVIDENCE", _clock.UtcNow);
        @case.MoveTo(CaseStage.ClosureReview, _clock.UtcNow);
        @case.Close(DomainTestData.TreatedDecision(_clock), _clock.UtcNow);
        return @case;
    }
    private Escalation Escalation(int level, string role = "facility-lead") => new()
    {
        Level = level, EscalatedTo = TestData.FacilityLeadQueue() with { Role = role },
        RaisedAt = _clock.UtcNow, Reason = "SYN late action",
    };
    private static CareLoopDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<CareLoopDbContext>();
    private static int Status(IResult result) => ((IStatusCodeHttpResult)result).StatusCode!.Value;
    private static T Value<T>(IResult result) => Assert.IsType<T>(((IValueHttpResult)result).Value);
    private static IReadOnlyList<T> Rows<T>(IResult result) => Assert.IsAssignableFrom<IReadOnlyList<T>>(((IValueHttpResult)result).Value);

    private sealed class UtcModelCustomizer(ModelCustomizerDependencies dependencies) : ModelCustomizer(dependencies)
    {
        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            base.Customize(modelBuilder, context);
            foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetProperties())
                .Where(p => (Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType) == typeof(DateTimeOffset)))
                property.SetValueConverter(new ValueConverter<DateTimeOffset, long>(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero)));
        }
    }
}

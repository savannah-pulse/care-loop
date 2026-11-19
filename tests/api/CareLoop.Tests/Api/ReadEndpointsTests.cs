using System.Text.Json;
using System.Text.Json.Serialization;
using CareLoop.Api.Features.Cases;
using CareLoop.Api.Features.Events;
using CareLoop.Api.Features.Events.Handlers;
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
using Microsoft.Extensions.Logging.Abstractions;

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
        var filtered = Rows<CaseDetailResponse>(await ListAsync(stage: CaseStage.FollowUpActive, owner: second.Owner.Reference, escalated: true));
        Assert.Equal(second.Id, Assert.Single(filtered).CaseId);
        Assert.Equal(first.Id, Assert.Single(Rows<CaseDetailResponse>(await ListAsync(escalated: false))).CaseId);
        Assert.Equal(second.Id, Rows<CaseDetailResponse>(await ListAsync(take: 1))[0].CaseId);
        Assert.Equal(first.Id, Rows<CaseDetailResponse>(await ListAsync(skip: 1, take: 1))[0].CaseId);
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
        @case.Escalate(Escalation(1));
        await SaveChangeAsync(@case.Id, c => c.Escalate(@case.Escalations.Single()));
        var listed = Assert.Single(Rows<CaseDetailResponse>(await ListAsync(context: Identity(DemoRoles.NurseNavigator))));
        Assert.Equal("SYN-STAFF-2", listed.ProposedOwner!.Reference);
        Assert.Equal(4, listed.Tracks.Count);
        Assert.Single(listed.Escalations);
        Assert.Equal(@case.SourceSystem, listed.SourceSystem);
        Assert.Equal(@case.SourceResultId, listed.SourceResultId);
        Assert.Equal(JsonSerializer.Serialize(Value<CaseDetailResponse>(await GetAsync(@case.Id)), JsonOptions),
            JsonSerializer.Serialize(listed, JsonOptions));
    }

    [Fact]
    public async Task Case_reads_use_each_patients_recorded_language_and_ignore_rejected_screening_updates()
    {
        var screening = Envelopes.ScreeningResult(_clock.UtcNow, namedOwner: Envelopes.Clinician());
        var arabic = await IngestAsync(screening with
        {
            Payload = ((ScreeningResultPayload)screening.Payload) with { PatientLanguage = "ar" },
        });
        _clock.Advance(TimeSpan.FromMinutes(1));
        var english = await IngestAsync(Envelopes.ScreeningResult(_clock.UtcNow, resultId: "SYN-RESULT-EN", namedOwner: Envelopes.Clinician()));
        _clock.Advance(TimeSpan.FromMinutes(1));
        await SaveChangeAsync(arabic.Id, _ => { }, CaseEventType.ScreeningResultReceived,
            payloadJson: "{\"patientLanguage\":\"ar-AE\"}");
        _clock.Advance(TimeSpan.FromMinutes(1));
        await SaveChangeAsync(arabic.Id, _ => { }, CaseEventType.ScreeningResultReceived,
            disposition: SourceEventDisposition.Rejected, payloadJson: "{\"patientLanguage\":\"fr\"}");

        var rows = Rows<CaseDetailResponse>(await ListAsync(context: Identity(DemoRoles.NurseNavigator)));
        Assert.Equal("ar-AE", rows.Single(c => c.CaseId == arabic.Id).PatientLanguage);
        Assert.Equal("en", rows.Single(c => c.CaseId == english.Id).PatientLanguage);
        var detail = Value<CaseDetailResponse>(await GetAsync(arabic.Id));
        Assert.Equal("ar-AE", detail.PatientLanguage);
        Assert.Contains("\"patientLanguage\":\"ar-AE\"", JsonSerializer.Serialize(detail, JsonOptions));
    }

    [Fact]
    public async Task Case_reads_leave_unknown_patient_language_null_rather_than_assuming_English()
    {
        var @case = await SeedAsync();
        Assert.Null(Assert.Single(Rows<CaseDetailResponse>(await ListAsync())).PatientLanguage);
        Assert.Null(Value<CaseDetailResponse>(await GetAsync(@case.Id)).PatientLanguage);
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
    public async Task Exceptions_still_lists_a_flagged_case_that_has_no_deadline_or_next_action()
    {
        var healthy = NewCase("SYN-RESULT-2", "SYN-FAC-A");
        healthy.FlagException(CaseExceptions.Rejected, _clock.UtcNow);
        await SeedAsync(healthy);
        var incomplete = new Case(Guid.NewGuid(), "SYN-HIS", "SYN-RESULT-3", "SYN-PT-0002", "breast-screening", "SYN-FAC-A",
            TestData.Clinician() with { FacilityId = "SYN-FAC-A" }, _clock.UtcNow);
        incomplete.FlagException(CaseExceptions.Unassigned, _clock.UtcNow);
        await SeedAsync(incomplete);

        var rows = Rows<ExceptionItemResponse>(await ExceptionsAsync());
        Assert.Equal(2, rows.Count);
        var row = rows.Single(r => r.CaseId == incomplete.Id);
        Assert.Null(row.NextAction);
        Assert.Null(row.DueAt);
        Assert.Null(row.OverdueBy);
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
        Assert.Empty(Rows<CaseDetailResponse>(await ListAsync()));
        Assert.Equal(403, Status(await GetAsync(foreign.Id)));
        Assert.Equal(403, Status(await TimelineAsync(foreign.Id)));
        Assert.Equal(403, Status(await ListAsync(facility: "SYN-FAC-B")));
        var response = Value<CaseDetailResponse>(await GetAsync(foreign.Id, reason: "SYN covering the other facility"));
        Assert.Equal("SYNTHETIC Patient B", response.PatientDisplayName);
        Assert.Equal(200, Status(await TimelineAsync(foreign.Id, reason: "SYN covering the other facility")));
        var rows = Rows<CaseDetailResponse>(await ListAsync(facility: "SYN-FAC-B", reason: "SYN covering the other facility"));
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
        _clock.Advance(TimeSpan.FromMinutes(1));
        await SaveChangeAsync(escalated.Id, _ => { }, CaseEventType.ScreeningResultReceived,
            payloadJson: "{\"patientLanguage\":\"ar\"}");
        var coordinator = Identity("coordinator", facility: null);
        Assert.Equal(403, Status(await GetAsync(normal.Id, coordinator)));
        var summary = Assert.Single(Rows<CaseDetailResponse>(await ListAsync(context: coordinator)));
        Assert.Equal(escalated.Id, summary.CaseId);
        Assert.Null(summary.PatientDisplayName);
        Assert.Null(summary.PatientSecondaryIdentifier);
        Assert.Null(summary.PatientReference);
        Assert.Null(summary.SourceResultId);
        Assert.Null(summary.PatientLanguage);
        var detail = Value<CaseDetailResponse>(await GetAsync(escalated.Id, coordinator));
        Assert.Null(detail.PatientDisplayName);
        Assert.Null(detail.PatientSecondaryIdentifier);
        Assert.Null(detail.PatientReference);
        Assert.Null(detail.SourceResultId);
        Assert.Null(detail.PatientLanguage);
        // Staff on the case's own team still see both.
        var lead = Value<CaseDetailResponse>(await GetAsync(escalated.Id, Identity("facility-lead", "SYN-FAC-B")));
        Assert.NotNull(lead.PatientReference);
        Assert.NotNull(lead.SourceResultId);
        Assert.Equal("ar", lead.PatientLanguage);
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
    public async Task Nurse_records_what_the_patient_said_on_a_call_without_changing_who_is_responsible()
    {
        var before = await NotifiedCaseAsync();

        var result = Value<CaseDetailResponse>(await RecordCallAsync(before.Id, new CallOutcomeRequest { Acknowledged = true, Understood = true, Confidence = 4 }));

        Assert.Equal(before.Owner.Reference, result.Owner.Reference);
        Assert.Equal(4, result.Confidence);
        Assert.Equal("en", result.PatientLanguage);
        var after = await ReadCaseAsync(before.Id);
        Assert.Equal(NotificationStatus.Understood, after.Notification!.Status);
        Assert.Equal(4, after.Notification.Confidence);
        Assert.Equal(before.Owner, after.Owner);
        Assert.Empty(after.Notification.Attempts);

        using var scope = _services.CreateScope();
        var recorded = await Db(scope).SourceEvents.AsNoTracking()
            .Where(e => e.CaseId == before.Id && e.SourceEventId.StartsWith("call-outcome/")).ToListAsync();
        Assert.Equal(3, recorded.Count);
        Assert.All(recorded, e => Assert.Equal(CaseEventType.NotificationUpdated, e.Type));
        Assert.All(recorded, e => Assert.Equal(SourceEventDisposition.Applied, e.Disposition));
        Assert.All(recorded, e => Assert.Equal(DemoRoles.NurseNavigator, e.Actor!.Role));
        Assert.All(recorded, e => Assert.Equal("SYN-STAFF-1", e.Actor!.Reference));
        Assert.All(recorded, e => Assert.DoesNotContain("\"channel\":\"", e.PayloadJson));
        Assert.Equal(3, await Db(scope).AuditEvents.CountAsync(a => a.CaseId == before.Id && a.ActorRole == DemoRoles.NurseNavigator));
    }

    [Fact]
    public async Task Understanding_and_a_rating_are_recorded_apart_from_an_acknowledgement()
    {
        var @case = await NotifiedCaseAsync();

        Assert.Equal(200, Status(await RecordCallAsync(@case.Id, new CallOutcomeRequest { Confidence = 2 })));
        var ratedOnly = await ReadCaseAsync(@case.Id);
        Assert.Equal(NotificationStatus.Sent, ratedOnly.Notification!.Status);
        Assert.Equal(2, ratedOnly.Notification.Confidence);

        Assert.Equal(200, Status(await RecordCallAsync(@case.Id, new CallOutcomeRequest { Understood = true })));
        var understood = await ReadCaseAsync(@case.Id);
        Assert.Equal(NotificationStatus.Understood, understood.Notification!.Status);
        Assert.Equal(2, understood.Notification.Confidence);

        // A later "she will come" on another call changes nothing that is already known, and is still accepted.
        Assert.Equal(200, Status(await RecordCallAsync(@case.Id, new CallOutcomeRequest { Acknowledged = true, Confidence = 5 })));
        var later = await ReadCaseAsync(@case.Id);
        Assert.Equal(NotificationStatus.Understood, later.Notification!.Status);
        Assert.Equal(5, later.Notification.Confidence);
    }

    [Theory]
    [InlineData("clinician")]
    [InlineData("receiving-clinician")]
    [InlineData("facility-lead")]
    [InlineData("coordinator")]
    [InlineData("lab")]
    public async Task Only_a_nurse_navigator_may_record_a_call_outcome(string role)
    {
        var before = await NotifiedCaseAsync();

        var refused = await RecordCallAsync(before.Id, new CallOutcomeRequest { Acknowledged = true }, Identity(role, role == "coordinator" ? null : "SYN-FAC-A"));

        Assert.Equal(403, Status(refused));
        var after = await ReadCaseAsync(before.Id);
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(NotificationStatus.Sent, after.Notification!.Status);
    }

    [Fact]
    public async Task Call_outcome_must_record_something_and_a_rating_must_be_from_1_to_5()
    {
        var before = await NotifiedCaseAsync();

        Assert.Equal(400, Status(await RecordCallAsync(before.Id, new CallOutcomeRequest())));
        Assert.Equal(400, Status(await RecordCallAsync(before.Id, new CallOutcomeRequest { Confidence = 0 })));
        Assert.Equal(400, Status(await RecordCallAsync(before.Id, new CallOutcomeRequest { Understood = true, Confidence = 6 })));
        Assert.Equal(401, Status(await RecordCallAsync(before.Id, new CallOutcomeRequest { Understood = true }, new DefaultHttpContext())));
        Assert.Equal(404, Status(await RecordCallAsync(Guid.NewGuid(), new CallOutcomeRequest { Understood = true })));

        Assert.Equal(before.Version, (await ReadCaseAsync(before.Id)).Version);
    }

    [Fact]
    public async Task Call_outcome_for_a_case_at_another_facility_needs_a_reason()
    {
        var @case = await NotifiedCaseAsync();
        var elsewhere = Identity(DemoRoles.NurseNavigator, "SYN-FAC-B");

        var withoutReason = await RecordCallAsync(@case.Id, new CallOutcomeRequest { Understood = true }, elsewhere);
        Assert.NotEqual(200, Status(withoutReason));
        Assert.Equal(NotificationStatus.Sent, (await ReadCaseAsync(@case.Id)).Notification!.Status);

        var withReason = await RecordCallAsync(@case.Id, new CallOutcomeRequest { Understood = true }, elsewhere, "SYN covering the clinic's calls today");
        Assert.Equal(200, Status(withReason));

        // The answer is the whole case, so both attempts are in the read audit, the second with its reason.
        using var scope = _services.CreateScope();
        var reads = await Db(scope).CaseReads.AsNoTracking().Where(r => r.CaseId == @case.Id && r.View.StartsWith(RecordCallOutcome.AuditView)).ToListAsync();
        Assert.Equal(2, reads.Count);
        Assert.All(reads, r => Assert.True(r.OutsideTeam));
        Assert.Single(reads, r => r.View == RecordCallOutcome.AuditView + "-denied" && r.Reason is null);
        Assert.Single(reads, r => r.View == RecordCallOutcome.AuditView && r.Reason == "SYN covering the clinic's calls today");
    }

    [Fact]
    public async Task Recording_a_call_answers_the_patients_request_for_one()
    {
        var @case = await NotifiedCaseAsync();
        var asked = await IngestAsync(Envelopes.Notification(_clock.UtcNow, NotificationUpdateKind.CallRequested));
        Assert.Equal(NotificationUpdateHandler.CallPatientAction, asked.NextAction);
        Assert.True(asked.Notification!.CallRequested);
        using (var outbox = _services.CreateScope())
        {
            // The pathway names the nurse navigator for the call, so they are told, whoever owns the case.
            var raised = Assert.Single(await Db(outbox).OutboxMessages.AsNoTracking().Where(m => m.CaseId == @case.Id && m.EventType == "CallRequested").ToListAsync());
            Assert.Contains("\"callerRole\":\"nurse-navigator\"", raised.PayloadJson);
        }

        // A text-message "will come" after asking for a call does not ring the patient back.
        var replied = await IngestAsync(Envelopes.Notification(_clock.UtcNow, NotificationUpdateKind.Acknowledged));
        Assert.Equal(NotificationUpdateHandler.CallPatientAction, replied.NextAction);
        Assert.True(replied.Notification!.CallRequested);

        _clock.Advance(TimeSpan.FromMinutes(30));
        Assert.Equal(200, Status(await RecordCallAsync(@case.Id, new CallOutcomeRequest { Understood = true })));

        var after = await ReadCaseAsync(@case.Id);
        Assert.False(after.Notification!.CallRequested);
        Assert.Equal(HandlerSupport.ArrangeFollowUpAction, after.NextAction);
        Assert.True(after.DueAt > asked.DueAt);
        Assert.Equal(@case.Owner, after.Owner);
    }

    [Fact]
    public async Task A_call_with_only_a_rating_goes_back_to_waiting_for_the_acknowledgement()
    {
        var @case = await NotifiedCaseAsync();
        await IngestAsync(Envelopes.Notification(_clock.UtcNow, NotificationUpdateKind.CallRequested));

        Assert.Equal(200, Status(await RecordCallAsync(@case.Id, new CallOutcomeRequest { Confidence = 3 })));

        var after = await ReadCaseAsync(@case.Id);
        Assert.False(after.Notification!.CallRequested);
        Assert.False(after.Notification.IsComplete());
        Assert.Equal(@case.NextAction, after.NextAction);
        Assert.Equal(3, after.Notification.Confidence);
    }

    [Fact]
    public async Task The_timeline_says_where_each_entry_came_from()
    {
        var @case = await NotifiedCaseAsync();
        await IngestAsync(Envelopes.Notification(_clock.UtcNow, NotificationUpdateKind.Delivered) with { Origin = EventOrigins.Sms });
        Assert.Equal(200, Status(await RecordCallAsync(@case.Id, new CallOutcomeRequest { Confidence = 4 })));

        var entries = Rows<TimelineEntryResponse>(await TimelineAsync(@case.Id));

        Assert.Equal(EventOrigins.Hospital, entries.First(e => e.EventType == CaseEventType.ScreeningResultReceived).Origin);
        Assert.Contains(entries, e => e.EventType == CaseEventType.NotificationUpdated && e.Origin == EventOrigins.Sms);
        Assert.Contains(entries, e => e.EventType == CaseEventType.NotificationUpdated && e.Origin == EventOrigins.CareLoop && e.ActorRole == DemoRoles.NurseNavigator);
        Assert.All(entries, e => Assert.Contains(e.Origin, new[] { EventOrigins.Hospital, EventOrigins.CareLoop, EventOrigins.Sms, EventOrigins.Email }));
    }

    [Fact]
    public async Task The_timeline_says_what_each_call_outcome_recorded()
    {
        var @case = await NotifiedCaseAsync();

        Assert.Equal(200, Status(await RecordCallAsync(@case.Id, new CallOutcomeRequest { Acknowledged = true, Understood = true, Confidence = 4 })));

        using var scope = _services.CreateScope();
        var reasons = await Db(scope).AuditEvents.AsNoTracking()
            .Where(a => a.CaseId == @case.Id && a.ActorRole == DemoRoles.NurseNavigator).Select(a => a.Reason).ToListAsync();
        Assert.Equal(3, reasons.Count);
        Assert.Contains("On a call, the patient said they will come.", reasons);
        Assert.Contains("On a call, the patient said the next step back in their own words.", reasons);
        Assert.Contains("On a call, the patient rated their confidence 4 out of 5.", reasons);
    }

    [Fact]
    public async Task Call_outcome_is_refused_and_audited_when_the_patient_was_never_notified()
    {
        var notNotified = await IngestAsync(Envelopes.ScreeningResult(_clock.UtcNow, namedOwner: Envelopes.Clinician()));
        Assert.Null(notNotified.Notification);

        var refused = await RecordCallAsync(notNotified.Id, new CallOutcomeRequest { Acknowledged = true, Understood = true });

        Assert.Equal(409, Status(refused));
        using var scope = _services.CreateScope();
        var attempt = Assert.Single(await Db(scope).SourceEvents.AsNoTracking().Where(e => e.SourceEventId.StartsWith("call-outcome/")).ToListAsync());
        Assert.Equal(SourceEventDisposition.Rejected, attempt.Disposition);
        Assert.Null((await ReadCaseAsync(notNotified.Id)).Notification);
    }

    [Fact]
    public async Task A_lead_of_another_facility_cannot_acknowledge_an_escalation_even_with_a_reason()
    {
        var @case = await SeedAsync();
        await SaveChangeAsync(@case.Id, c => { c.FlagException(CaseExceptions.Overdue, _clock.UtcNow); c.Escalate(Escalation(1)); });
        var id = (await ReadCaseAsync(@case.Id)).Escalations.Single().Id;

        var refused = await AcknowledgeAsync(@case.Id, id, Identity("facility-lead", "SYN-FAC-B"), "SYN documented cross-facility reason");
        Assert.Equal(403, Status(refused));
        Assert.False((await ReadCaseAsync(@case.Id)).Escalations.Single().IsAcknowledged);

        var accepted = Value<CaseDetailResponse>(await AcknowledgeAsync(@case.Id, id, Identity("facility-lead", "SYN-FAC-A")));
        Assert.Equal("facility-lead", accepted.Escalations.Single().AcknowledgedByRole);
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
        Assert.Equal(@case.Id, Assert.Single(Rows<CaseDetailResponse>(await ListAsync())).CaseId);
    }

    [Fact]
    public async Task A_step_dated_before_its_screening_result_is_left_out_of_that_measure_not_fatal()
    {
        await IngestAsync(Envelopes.ScreeningResult(_clock.UtcNow.AddHours(1), namedOwner: Envelopes.Clinician()));
        await IngestAsync(Envelopes.ClinicalReview(_clock.UtcNow));
        await IngestAsync(Envelopes.Notification(_clock.UtcNow, NotificationUpdateKind.Acknowledged));

        var metrics = Value<MetricsResponse>(await MetricsAsync());
        Assert.Equal(1, metrics.TotalCases);
        Assert.Equal(0, metrics.TimeToStep["acknowledgement"].Denominator);
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
        return await GetExceptions.Handle(null, null, null, null, null, Identity(), Db(scope), _pathways, _clock, CancellationToken.None);
    }

    private async Task<IResult> MetricsAsync(HttpContext? context = null)
    {
        using var scope = _services.CreateScope();
        return await GetMetrics.Handle(null, null, context ?? Identity(), Db(scope), _pathways, _clock,
            NullLoggerFactory.Instance, CancellationToken.None);
    }

    private async Task<IResult> AcknowledgeAsync(Guid caseId, Guid escalationId, HttpContext context, string? reason = null)
    {
        using var scope = _services.CreateScope();
        return await AcknowledgeEscalation.Handle(caseId, escalationId, reason, context,
            scope.ServiceProvider.GetRequiredService<ICaseRepository>(), Db(scope), _pathways, _clock, CancellationToken.None);
    }

    /// <summary>A reviewed case, so its patient notification has been requested and nothing has come back yet.</summary>
    private async Task<Case> NotifiedCaseAsync()
    {
        await IngestAsync(Envelopes.ScreeningResult(_clock.UtcNow, namedOwner: Envelopes.Clinician()));
        _clock.Advance(TimeSpan.FromHours(1));
        var reviewed = await IngestAsync(Envelopes.ClinicalReview(_clock.UtcNow));
        _clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(NotificationStatus.Sent, reviewed.Notification!.Status);
        return reviewed;
    }

    private async Task<IResult> RecordCallAsync(Guid caseId, CallOutcomeRequest request, HttpContext? context = null, string? reason = null)
    {
        using var scope = _services.CreateScope();
        return await RecordCallOutcome.Handle(caseId, reason, request, context ?? Identity(DemoRoles.NurseNavigator),
            scope.ServiceProvider.GetRequiredService<ICaseRepository>(), scope.ServiceProvider.GetRequiredService<EventIngestor>(),
            Db(scope), _pathways, _clock, CancellationToken.None);
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
        string source = "SYN-HIS", SourceEventDisposition disposition = SourceEventDisposition.Applied, string payloadJson = "{}")
    {
        using var scope = _services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ICaseRepository>();
        var @case = (await repo.GetAsync(id, CancellationToken.None))!;
        change(@case);
        await repo.SaveAsync(@case, TestData.AppliedEvent(@case, Guid.NewGuid().ToString("N"), _clock.UtcNow, type)
            with { SourceSystem = source, Disposition = disposition, DispositionReason = "SYN test event", PayloadJson = payloadJson }, CancellationToken.None);
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

using System.Text.Json;
using System.Text.Json.Serialization;
using CareLoop.Api.Features.Agents;
using CareLoop.Api.Features.Events;
using CareLoop.Api.Features.Notifications;
using CareLoop.Api.Infrastructure;
using CareLoop.Api.Infrastructure.Ai;
using CareLoop.Api.Infrastructure.Auth;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Api.Infrastructure.Persistence.Outbox;
using CareLoop.Contracts.Ai;
using CareLoop.Contracts.Events;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Recipients;
using CareLoop.Domain.Time;
using CareLoop.Domain.Tracks;
using CareLoop.Tests.Api.Outbox;
using CareLoop.Tests.Domain;
using CareLoop.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CareLoop.Tests.Api.Agents;

/// <summary>
/// The three AI endpoints: switched off they answer 409, the agent's text comes back as a draft with its agent and
/// prompt versions, a failing agent is a calm 503, access follows the reads, each call is audited with its agent version,
/// prompt version and correlation identifier, and no prompt ever carries a patient's name, record number, reference,
/// contact details, the hospital's identifiers or free text; the patient message prompt never carries a next action
/// that would reveal a result.
/// </summary>
public sealed class AiAgentEndpointsTests : IDisposable
{
    private const string PatientName = "SYNTHETIC Hessa Example";
    private const string PatientMrn = "SYN-MRN-99887766";
    private const string PatientReference = "SYN-PT-0001";
    private const string HospitalResultId = "SYN-RESULT-HOSPITAL-4242";
    private const string AppointmentPlace = "SYN Breast Clinic, Room 4";
    private const string EscalationReason = "SYN free text the briefing must never carry";
    private const string Reader = "SYN-STAFF-1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    /// <summary>Next actions whose staff wording would tell a patient something about a result.</summary>
    private static readonly string[] ResultBearing =
    [
        "review-result", "answer-consultation", "respond-to-referral", "arrange-follow-up", "reassign-consultation",
        "arrange-alternative-referral", "start-treatment", "decide-closure",
    ];

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly FakeClock _clock = new();
    private readonly ServiceProvider _services;
    private readonly IPathwayProvider _pathways = DomainTestData.Provider();
    private readonly FakeAgentClient _agents = new();
    private readonly AgentOptions _options = new() { Enabled = true, UrgentCodes = ["PLACEHOLDER-URGENCY-B"] };
    private readonly MessageFormat _format = new(new NotificationOptions
    {
        FacilityNames = new(StringComparer.OrdinalIgnoreCase) { ["SYN-FAC-A"] = "SYN Family Health Centre", ["SYN-FAC-B"] = "SYN General Hospital" },
    });

    public AiAgentEndpointsTests()
    {
        _connection.Open();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IClock>(_clock);
        services.AddSingleton(_pathways);
        services.AddDbContext<CareLoopDbContext>(o => o.UseSqlite(_connection).ReplaceService<IModelCustomizer, SqliteDateTimeOffsetCustomizer>());
        services.AddSingleton(new OutboxSubscriberRegistry(() => ["FhirPublisher", "NotificationRouter"]));
        services.AddScoped<ICaseRepository, EfCaseRepository>();
        services.AddIntake();
        _services = services.BuildServiceProvider();
        using var scope = _services.CreateScope();
        Db(scope).Database.EnsureCreated();
    }

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }

    [Theory]
    [InlineData(false, true, "Ai:Agents:Enabled is false")]
    [InlineData(true, false, "Ai:Provider is NoOp")]
    public async Task Switched_off_every_endpoint_answers_409_reads_nothing_and_asks_no_agent(bool enabled, bool routed, string why)
    {
        var @case = await NamedCaseAsync();
        _options.Enabled = enabled;
        _agents.IsEnabled = routed;

        foreach (var result in new[]
        {
            await BriefingAsync(), await ImpactAsync(), await DraftAsync(@case.Id, Identity(DemoRoles.NurseNavigator)),
        })
        {
            Assert.Equal(409, Status(result));
            var problem = Assert.IsType<ProblemDetails>(((IValueHttpResult)result).Value);
            Assert.Equal("AI is switched off", problem.Title);
            Assert.Contains(why, problem.Detail);
        }

        Assert.Empty(_agents.Calls);
        using var scope = _services.CreateScope();
        Assert.Empty(await Db(scope).CaseReads.ToListAsync());
    }

    [Fact]
    public async Task The_agents_text_comes_back_as_a_draft_with_the_agent_and_prompt_versions_and_the_time()
    {
        var @case = await NamedCaseAsync();
        _agents.Answer = agent => $"  SYN words from {agent}.  ";

        var briefing = Value<AiTextResponse>(await BriefingAsync());
        var impact = Value<AiTextResponse>(await ImpactAsync());
        var draft = Value<AiTextResponse>(await DraftAsync(@case.Id, Identity(DemoRoles.NurseNavigator)));

        Assert.Equal(("role-briefing", "2", "role-briefing-v1", "SYN words from role-briefing."),
            (briefing.Agent, briefing.AgentVersion, briefing.PromptVersion, briefing.Text));
        Assert.Equal(("impact-summary", "4", "impact-summary-v1", "SYN words from impact-summary."),
            (impact.Agent, impact.AgentVersion, impact.PromptVersion, impact.Text));
        Assert.Equal(("patient-notification", "2", "patient-message-v2", "SYN words from patient-notification."),
            (draft.Agent, draft.AgentVersion, draft.PromptVersion, draft.Text));
        Assert.All([briefing, impact, draft], r => Assert.Equal(_clock.UtcNow, r.GeneratedAt));
        Assert.Equal(["role_briefing", "impact_summary", "patient_message_preview"], _agents.Calls.Select(c => c.Purpose));
        Assert.All(_agents.Calls, c => Assert.StartsWith($"CareLoop", c.Prompt));
        Assert.All(_agents.Calls, c => Assert.Contains($"prompt version {c.PromptVersion}.", c.Prompt));
    }

    [Fact]
    public async Task Each_call_is_audited_with_the_agent_version_the_prompt_version_and_its_correlation_identifier()
    {
        var @case = await NamedCaseAsync();

        Assert.Equal(200, Status(await BriefingAsync()));
        Assert.Equal(200, Status(await ImpactAsync()));
        Assert.Equal(200, Status(await DraftAsync(@case.Id, Identity(DemoRoles.NurseNavigator))));

        using var scope = _services.CreateScope();
        var reads = await Db(scope).CaseReads.OrderBy(r => r.View).ToListAsync();
        Assert.Equal(3, reads.Count);
        foreach (var call in _agents.Calls)
        {
            var read = Assert.Single(reads, r => r.CorrelationId == call.CorrelationId);
            Assert.Equal((call.Agent.Name, call.Agent.Version, call.PromptVersion), (read.AgentName, read.AgentVersion, read.PromptVersion));
        }

        Assert.Equal(3, _agents.Calls.Select(c => c.CorrelationId).Distinct().Count());
    }

    [Fact]
    public async Task No_prompt_carries_the_patients_name_identifiers_contact_details_or_free_text()
    {
        var @case = await NamedCaseAsync();
        var stored = await ReadCaseAsync(@case.Id);
        Assert.Equal(PatientName, stored.PatientDisplayName);
        Assert.Equal(PatientMrn, stored.PatientSecondaryIdentifier);
        Assert.Equal(PatientReference, stored.PatientReference);
        Assert.Equal(HospitalResultId, stored.SourceResultId);

        Assert.Equal(200, Status(await BriefingAsync()));
        Assert.Equal(200, Status(await ImpactAsync()));
        Assert.Equal(200, Status(await DraftAsync(@case.Id, Identity(DemoRoles.NurseNavigator), NotificationChannel.Sms)));
        Assert.Equal(200, Status(await DraftAsync(@case.Id, Identity(DemoRoles.Clinician), NotificationChannel.Email)));

        Assert.Equal(4, _agents.Calls.Count);
        Assert.Contains(Number(@case), _agents.Calls[0].Prompt);
        foreach (var call in _agents.Calls)
        {
            foreach (var secret in new[]
            {
                PatientName, "Hessa", PatientMrn, "99887766", PatientReference, HospitalResultId, "HOSPITAL-4242", Reader, "SYN-STAFF-0001",
                Envelopes.PatientPhone, Envelopes.PatientEmail, AppointmentPlace, "Room 4", EscalationReason, "SYN free text", stored.Id.ToString(),
            })
            {
                Assert.DoesNotContain(secret, call.Prompt, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Theory]
    [MemberData(nameof(EveryNextAction))]
    public void The_patient_message_prompt_never_carries_a_result_bearing_next_action(string nextAction)
    {
        var @case = NewCase("SYN-RESULT-STEP", "SYN-FAC-A");
        @case.SetNextAction(nextAction, _clock.UtcNow.AddDays(1), _clock.UtcNow);

        var prompt = DraftPatientMessage.BuildPrompt(@case, placeNamed: true, NotificationChannel.Sms, _format, _clock.UtcNow);

        var step = Assert.Single(prompt.Split('\n'), line => line.StartsWith("Next step for the patient: ", StringComparison.Ordinal))
            ["Next step for the patient: ".Length..].Trim();
        Assert.Contains(step, DraftPatientMessage.PatientSafeSteps);
        foreach (var action in _pathways.Get("breast-screening").Deadlines.Select(d => d.NextAction).Distinct())
        {
            Assert.DoesNotContain(action, prompt, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(AgentWords.NextAction(action), prompt, StringComparison.OrdinalIgnoreCase);
        }

        if (ResultBearing.Contains(nextAction) || nextAction.StartsWith("SYN-", StringComparison.Ordinal))
        {
            Assert.Equal(DraftPatientMessage.PatientSafeSteps[0], step);
        }

        Assert.Equal(DraftPatientMessage.PatientSafeSteps[0], DraftPatientMessage.PatientStepFor(null));
    }

    /// <summary>Every next action the pathway defines and an unknown one, so a new next action is covered as soon as it is added.</summary>
    public static TheoryData<string> EveryNextAction()
    {
        var data = new TheoryData<string> { "SYN-unknown-next-action" };
        foreach (var action in DomainTestData.Provider().Get("breast-screening").Deadlines.Select(d => d.NextAction).Distinct())
        {
            data.Add(action);
        }

        return data;
    }

    [Fact]
    public async Task The_briefing_counts_the_worklist_and_lists_cases_overdue_or_escalated_then_urgent_then_due_today()
    {
        var late = await NamedCaseAsync();
        var urgent = await SeedAsync(NewCase("SYN-RESULT-URGENT", "SYN-FAC-A", urgency: "PLACEHOLDER-URGENCY-B", due: _clock.UtcNow.AddDays(2)));
        var today = await SeedAsync(NewCase("SYN-RESULT-TODAY", "SYN-FAC-A", due: _clock.UtcNow.AddHours(3)));
        var later = await SeedAsync(NewCase("SYN-RESULT-LATER", "SYN-FAC-A", due: _clock.UtcNow.AddDays(3)));
        var elsewhere = await SeedAsync(NewCase("SYN-RESULT-ELSEWHERE", "SYN-FAC-B", due: _clock.UtcNow.AddHours(1)));

        Assert.Equal(200, Status(await BriefingAsync()));

        var prompt = Assert.Single(_agents.Calls).Prompt;
        Assert.Contains("Reader: clinician.", prompt);
        Assert.Contains("Facility: SYN Family Health Centre, the reader's team.", prompt);
        Assert.Contains("Open cases: 4", prompt);
        Assert.Contains("Overdue: 1", prompt);
        Assert.Contains("Escalated: 1", prompt);
        Assert.Contains("Urgent: 1", prompt);
        Assert.Contains("Due today: 1", prompt);
        var first = prompt.IndexOf(Number(late), StringComparison.Ordinal);
        var second = prompt.IndexOf(Number(urgent), StringComparison.Ordinal);
        var third = prompt.IndexOf(Number(today), StringComparison.Ordinal);
        Assert.True(first >= 0 && first < second && second < third, prompt);
        Assert.DoesNotContain(Number(later), prompt);
        Assert.DoesNotContain(Number(elsewhere), prompt);
        Assert.Contains("Next action: Make sure the patient has the message.", prompt);
        Assert.Contains("Flags: overdue, referral declined, escalated.", prompt);
        Assert.Contains("Acts next: a clinician.", prompt);
    }

    [Fact]
    public async Task The_coordinator_is_briefed_only_on_cases_escalated_to_the_coordinator()
    {
        var facilityLevel = await SeedAsync(NewCase("SYN-RESULT-LEAD", "SYN-FAC-A", due: _clock.UtcNow.AddHours(-2)));
        await ChangeAsync(facilityLevel.Id, c => c.Escalate(Escalation(1, DemoRoles.FacilityLead, "SYN-FAC-A")));
        var coordinatorLevel = await SeedAsync(NewCase("SYN-RESULT-COORD", "SYN-FAC-B", due: _clock.UtcNow.AddHours(-30)));
        await ChangeAsync(coordinatorLevel.Id, c => c.Escalate(Escalation(2, DemoRoles.Coordinator, "SYN-FAC-B")));
        await SeedAsync(NewCase("SYN-RESULT-QUIET", "SYN-FAC-B", due: _clock.UtcNow.AddDays(1)));

        Assert.Equal(200, Status(await BriefingAsync(Identity(DemoRoles.Coordinator, facility: null))));

        var prompt = Assert.Single(_agents.Calls).Prompt;
        Assert.Contains("Reader: programme coordinator.", prompt);
        Assert.Contains("only the cases escalated to the programme coordinator", prompt);
        Assert.Contains("Open cases: 1", prompt);
        Assert.Contains(Number(coordinatorLevel), prompt);
        Assert.DoesNotContain(Number(facilityLevel), prompt);
    }

    [Fact]
    public async Task The_briefing_says_which_cases_the_reader_acts_on_whether_or_not_the_header_names_a_FHIR_type()
    {
        var mine = await SeedAsync(NewCase("SYN-RESULT-MINE", "SYN-FAC-A", due: _clock.UtcNow.AddHours(-1)));
        await ChangeAsync(mine.Id, c => c.AssignOwner(TestData.Clinician(Reader), _clock.UtcNow));
        var context = Identity();
        context.Request.Headers[DemoRoles.UserHeader] = $"Practitioner/{Reader}";

        Assert.Equal(200, Status(await BriefingAsync(context)));

        Assert.Contains($"{Number(mine)} Programme: breast screening.", _agents.Calls.Single().Prompt);
        Assert.Contains("Acts next: the reader.", _agents.Calls.Single().Prompt);
    }

    [Theory]
    [InlineData(DemoRoles.Clinician, 200)]
    [InlineData(DemoRoles.ReceivingClinician, 403)]
    [InlineData(DemoRoles.FacilityLead, 403)]
    [InlineData(DemoRoles.Lab, 403)]
    [InlineData(DemoRoles.Coordinator, 403)]
    public async Task Only_a_nurse_navigator_or_a_clinician_may_preview_a_message_to_the_patient(string role, int status)
    {
        var @case = await NamedCaseAsync();

        Assert.Equal(status, Status(await DraftAsync(@case.Id, Identity(role, role == DemoRoles.Coordinator ? null : "SYN-FAC-A"))));
        Assert.Equal(status == 200 ? 1 : 0, _agents.Calls.Count);
    }

    [Fact]
    public async Task The_message_draft_uses_a_patient_safe_step_the_appointment_time_the_channel_and_the_reply_codes_and_is_recorded_as_a_read()
    {
        var @case = await NamedCaseAsync();

        Assert.Equal(200, Status(await DraftAsync(@case.Id, Identity(DemoRoles.NurseNavigator), NotificationChannel.Email)));

        var prompt = Assert.Single(_agents.Calls).Prompt;
        Assert.Contains("{first name}", prompt);
        Assert.Contains("Channel: email.", prompt);
        Assert.Contains("category A, administrative message", prompt);
        Assert.Contains("Next step for the patient: Your care team sent you a message and would like to know that you got it.", prompt);
        Assert.Contains("Appointment: Sunday 4 January 2026 at 10:30, local time. Place: {place}.", prompt);
        Assert.Contains("Reply 1 to confirm you got this message.", prompt);
        Assert.Contains("Reply 2 if you would like your care team to call you.", prompt);
        Assert.Contains("Never mention or hint at a screening result", prompt);
        Assert.DoesNotContain("Make sure the patient has the message", prompt);

        using var scope = _services.CreateScope();
        var read = Assert.Single(await Db(scope).CaseReads.ToListAsync());
        Assert.Equal((DraftPatientMessage.AuditView, @case.Id, "channel=Email"), (read.View, read.CaseId, read.Query));
        var unchanged = await ReadCaseAsync(@case.Id);
        Assert.Equal(@case.Version, unchanged.Version);
    }

    [Fact]
    public async Task Access_follows_the_case_reads_unknown_cases_404_and_other_teams_need_a_reason()
    {
        var foreign = await SeedAsync(NewCase("SYN-RESULT-FOREIGN", "SYN-FAC-B", due: _clock.UtcNow.AddHours(4)));

        Assert.Equal(404, Status(await DraftAsync(Guid.NewGuid(), Identity(DemoRoles.Clinician))));
        Assert.Equal(403, Status(await DraftAsync(foreign.Id, Identity(DemoRoles.Clinician))));
        Assert.Equal(200, Status(await DraftAsync(foreign.Id, Identity(DemoRoles.Clinician), reason: "SYN covering the other team")));
        Assert.Equal(403, Status(await BriefingAsync(facilityId: "SYN-FAC-B")));
        Assert.Equal(200, Status(await BriefingAsync(facilityId: "SYN-FAC-B", reason: "SYN covering the other team")));
        Assert.Equal(403, Status(await ImpactAsync(facilityId: "SYN-FAC-B")));
        Assert.Equal(401, Status(await BriefingAsync(new DefaultHttpContext())));

        Assert.Contains("another team's cases", _agents.Calls[^1].Prompt);
        using var scope = _services.CreateScope();
        var reads = await Db(scope).CaseReads.ToListAsync();
        Assert.Equal(3, reads.Count(r => r.View.EndsWith("-denied", StringComparison.Ordinal)));
        Assert.All(reads.Where(r => r.Reason is not null), r => Assert.True(r.OutsideTeam));
        Assert.All(reads.Where(r => r.View.EndsWith("-denied", StringComparison.Ordinal) || r.View == DraftPatientMessage.AuditView && r.CaseId is { } id && id != foreign.Id),
            r => Assert.Null(r.CorrelationId));
    }

    [Fact]
    public async Task The_impact_prompt_holds_totals_only()
    {
        var @case = await NamedCaseAsync();

        Assert.Equal(200, Status(await ImpactAsync()));

        var prompt = Assert.Single(_agents.Calls).Prompt;
        Assert.Contains("Scope: SYN Family Health Centre.", prompt);
        Assert.Contains("Cases: 1 in total, 1 open and 0 closed.", prompt);
        Assert.Contains("Open cases past their deadline: 1", prompt);
        Assert.Contains("- Patients who acknowledged the message: 0 of 1 patients told (0%).", prompt);
        Assert.DoesNotContain(Number(@case), prompt);
        Assert.DoesNotContain("Case ", prompt);
    }

    [Fact]
    public async Task A_failing_agent_is_a_calm_503_with_no_text()
    {
        var @case = await NamedCaseAsync();
        _agents.Failure = new AgentUnavailableException("role-briefing", "The agent answered HTTP 403.", 403);

        foreach (var result in new[] { await BriefingAsync(), await ImpactAsync(), await DraftAsync(@case.Id, Identity(DemoRoles.NurseNavigator)) })
        {
            Assert.Equal(503, Status(result));
            var problem = Assert.IsType<ProblemDetails>(((IValueHttpResult)result).Value);
            Assert.Equal("AI could not write this", problem.Title);
            Assert.DoesNotContain("SYN", JsonSerializer.Serialize(problem, JsonOptions));
        }

        _agents.Failure = null;
        _agents.Answer = _ => "   ";
        Assert.Equal(503, Status(await BriefingAsync()));
    }

    /// <summary>A reviewed case with the patient's name, MRN and the hospital's result id, an upcoming appointment, overdue, declined and escalated.</summary>
    private async Task<Case> NamedCaseAsync()
    {
        await IngestAsync(Envelopes.ScreeningResult(_clock.UtcNow, resultId: HospitalResultId, namedOwner: Envelopes.Clinician(),
            patientDisplayName: PatientName, patientSecondaryIdentifier: PatientMrn));
        await IngestAsync(Envelopes.ClinicalReview(_clock.UtcNow, resultId: HospitalResultId));
        var booked = await IngestAsync(Envelopes.Appointment(_clock.UtcNow, AppointmentStatus.Scheduled,
            scheduledFor: new DateTimeOffset(2026, 1, 4, 10, 30, 0, TimeSpan.Zero), resultId: HospitalResultId, location: AppointmentPlace));
        await ChangeAsync(booked.Id, c =>
        {
            c.FlagException(CaseExceptions.Overdue | CaseExceptions.Rejected, _clock.UtcNow);
            c.Escalate(Escalation(1, DemoRoles.FacilityLead, "SYN-FAC-A"));
        });
        _clock.Advance(TimeSpan.FromDays(1) + TimeSpan.FromHours(2));
        return await ReadCaseAsync(booked.Id);
    }

    private Case NewCase(string result, string facility, string? urgency = null, DateTimeOffset? due = null)
    {
        var @case = new Case(Guid.NewGuid(), "SYN-HIS", result, "SYN-PT-0002", "breast-screening", facility,
            TestData.Clinician() with { FacilityId = facility }, _clock.UtcNow, "SYNTHETIC Other Patient", "SYN-MRN-2");
        @case.MoveTo(CaseStage.ReviewPending, _clock.UtcNow);
        @case.SetNextAction("review-result", due ?? _clock.UtcNow.AddDays(1), _clock.UtcNow);
        if (urgency is not null) @case.RecordReview(urgency, DomainTestData.ClinicianActor(), _clock.UtcNow);
        return @case;
    }

    private Escalation Escalation(int level, string role, string facility) => new()
    {
        Level = level, EscalatedTo = TestData.FacilityLeadQueue() with { Role = role, FacilityId = facility },
        RaisedAt = _clock.UtcNow, Reason = EscalationReason,
    };

    private static DefaultHttpContext Identity(string role = DemoRoles.Clinician, string? facility = "SYN-FAC-A")
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[DemoRoles.UserHeader] = Reader;
        context.Request.Headers[DemoRoles.RoleHeader] = role;
        if (facility is not null) context.Request.Headers[DemoRoles.FacilityHeader] = facility;
        return context;
    }

    private async Task<IResult> BriefingAsync(HttpContext? context = null, string? facilityId = null, string? reason = null)
    {
        using var scope = _services.CreateScope();
        return await DraftRoleBriefing.Handle(facilityId, reason, context ?? Identity(), Db(scope), _pathways, _format, _agents,
            Options.Create(_options), _clock, CancellationToken.None);
    }

    private async Task<IResult> ImpactAsync(HttpContext? context = null, string? facilityId = null, string? reason = null)
    {
        using var scope = _services.CreateScope();
        return await DraftImpactSummary.Handle(facilityId, reason, context ?? Identity(), Db(scope), _pathways, _format, _agents,
            Options.Create(_options), _clock, NullLoggerFactory.Instance, CancellationToken.None);
    }

    private async Task<IResult> DraftAsync(Guid caseId, HttpContext context, NotificationChannel channel = NotificationChannel.Sms, string? reason = null)
    {
        using var scope = _services.CreateScope();
        return await DraftPatientMessage.Handle(caseId, context, Db(scope), _format, _agents, Options.Create(_options), _clock,
            CancellationToken.None, channel, reason);
    }

    private async Task<Case> SeedAsync(Case @case)
    {
        @case.ClearDomainEvents();
        using var scope = _services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ICaseRepository>().SaveAsync(@case,
            TestData.AppliedEvent(@case, Guid.NewGuid().ToString("N"), _clock.UtcNow), CancellationToken.None);
        return @case;
    }

    private async Task ChangeAsync(Guid id, Action<Case> change)
    {
        using var scope = _services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICaseRepository>();
        var @case = (await repository.GetAsync(id, CancellationToken.None))!;
        change(@case);
        @case.ClearDomainEvents();
        await repository.SaveAsync(@case, TestData.AppliedEvent(@case, Guid.NewGuid().ToString("N"), _clock.UtcNow,
            CareLoop.Domain.Events.CaseEventType.DeadlineElapsed) with { SourceSystem = "careloop" }, CancellationToken.None);
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

    private static string Number(Case @case) => $"Case {NotificationRouter.CaseReferenceFor(@case.Id)}.";
    private static CareLoopDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<CareLoopDbContext>();
    private static int Status(IResult result) => ((IStatusCodeHttpResult)result).StatusCode!.Value;
    private static T Value<T>(IResult result) => Assert.IsType<T>(((IValueHttpResult)result).Value);

    /// <summary>Stands in for the route <c>Ai:Provider</c> chooses: records every request and answers, or fails, as the test says.</summary>
    private sealed class FakeAgentClient : IAgentClient
    {
        public List<AgentRequest> Calls { get; } = [];

        public Func<string, string> Answer { get; set; } = agent => $"SYN text from {agent}.";

        public AgentUnavailableException? Failure { get; set; }

        public string Route => "Fake";

        public bool IsEnabled { get; set; } = true;

        public Task<AgentText> AskAsync(AgentRequest request, CancellationToken ct)
        {
            Calls.Add(request);
            return Failure is not null
                ? Task.FromException<AgentText>(Failure)
                : Task.FromResult(new AgentText(Answer(request.Agent.Name), request.Agent));
        }
    }
}

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
using CareLoop.Contracts.Metrics;
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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CareLoop.Tests.Api.Agents;

/// <summary>
/// The three AI endpoints: switched off they answer 409, the agent's text comes back as a draft with its agent and
/// prompt versions, a failing agent is a calm 503, access follows the reads, each call is audited with its agent version,
/// prompt version and correlation identifier, and no prompt ever carries a patient's name, record number, reference,
/// contact details, the hospital's identifiers or free text; the patient message prompt never carries a next action
/// that would reveal a result, and the impact summary hides small numbers.
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
    private readonly ListLoggerFactory _logs = new();
    private readonly AgentOptions _options = new() { Enabled = true, UrgentCodes = ["PLACEHOLDER-URGENCY-B"] };
    private readonly AiOptions _ai = new() { TimeoutSeconds = 30 };
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
    public async Task The_agents_answer_comes_back_as_a_draft_in_plain_words_with_the_agent_and_prompt_versions_and_the_time()
    {
        var @case = await NamedCaseAsync();

        var briefing = Value<AiTextResponse>(await BriefingAsync());
        var impact = Value<AiTextResponse>(await ImpactAsync());
        var draft = Value<AiTextResponse>(await DraftAsync(@case.Id, Identity(DemoRoles.NurseNavigator)));

        Assert.Equal(("role-briefing", "2", "role-briefing-v3", $"SYN headline.\n\nSYN P1: case {NotificationRouter.CaseReferenceFor(@case.Id)}."),
            (briefing.Agent, briefing.AgentVersion, briefing.PromptVersion, briefing.Text));
        Assert.Equal(("impact-summary", "4", "impact-summary-v3", "SYN sentence.\n\nSYN limits note."),
            (impact.Agent, impact.AgentVersion, impact.PromptVersion, impact.Text));
        Assert.Equal(("patient-notification", "2", "patient-message-v3",
                "Hello {first name}, this is your care team at SYN Family Health Centre. Your care team sent you a message and would like to know that you got it. "
                + "Your appointment is on Sunday 4 January 2026 at 10:30. Place: {place}. "
                + "Reply 1 to confirm you got this message. Reply 2 if you would like your care team to call you. Thank you."),
            (draft.Agent, draft.AgentVersion, draft.PromptVersion, draft.Text));
        Assert.All([briefing, impact, draft], r => Assert.Equal(_clock.UtcNow, r.GeneratedAt));
        Assert.Equal(["role_briefing", "impact_summary", "patient_message_preview"], _agents.Calls.Select(c => c.Purpose));
        Assert.All(_agents.Calls, c => Assert.StartsWith($"CareLoop", c.Prompt));
        Assert.All(_agents.Calls, c => Assert.Contains($"prompt version {c.PromptVersion}.\n<input_json>\n", c.Prompt));
        Assert.All(_agents.Calls, c => Assert.EndsWith("\n</input_json>\n", c.Prompt));
    }

    [Fact]
    public async Task A_refusal_is_the_calm_503_with_its_reason_code_in_the_log_and_not_on_screen()
    {
        var @case = await NamedCaseAsync();
        _agents.Answer = _ => "{\"cannot_comply\":{\"reason_code\":\"MISSING_FIELD\",\"detail\":\"SYN detail about the input\"}}";

        foreach (var result in new[] { await BriefingAsync(), await ImpactAsync(), await DraftAsync(@case.Id, Identity(DemoRoles.NurseNavigator)) })
        {
            Assert.Equal(503, Status(result));
            var problem = JsonSerializer.Serialize(Assert.IsType<ProblemDetails>(((IValueHttpResult)result).Value), JsonOptions);
            Assert.Contains("AI could not write this", problem);
            Assert.DoesNotContain("MISSING_FIELD", problem);
            Assert.DoesNotContain("SYN detail", problem);
        }

        Assert.Equal(3, _agents.Calls.Count);
        Assert.All(_agents.Calls, call => Assert.Single(_logs.Lines, line =>
            line.Contains(call.CorrelationId.ToString(), StringComparison.Ordinal) && line.EndsWith(": MISSING_FIELD", StringComparison.Ordinal)));
        Assert.DoesNotContain(_logs.Lines, line => line.Contains("SYN detail", StringComparison.Ordinal));
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
        Assert.Contains("\"token\":\"P-1\"", _agents.Calls[0].Prompt);
        foreach (var call in _agents.Calls)
        {
            foreach (var secret in new[]
            {
                PatientName, "Hessa", PatientMrn, "99887766", PatientReference, HospitalResultId, "HOSPITAL-4242", Reader, "SYN-STAFF-0001",
                Envelopes.PatientPhone, Envelopes.PatientEmail, AppointmentPlace, "Room 4", EscalationReason, "SYN free text", stored.Id.ToString(),
                stored.Id.ToString("N"), NotificationRouter.CaseReferenceFor(stored.Id),
            })
            {
                Assert.DoesNotContain(secret, call.Prompt, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public async Task An_answer_in_the_wrong_format_is_asked_again_each_attempt_audited_with_its_own_correlation_identifier()
    {
        await NamedCaseAsync();
        _agents.Answers.Enqueue("Here is the briefing you asked for: SYN prose, not JSON.");
        _agents.Answers.Enqueue("""{"headline":"SYN headline without sections."}""");

        var briefing = Value<AiTextResponse>(await BriefingAsync());

        Assert.StartsWith("SYN headline.", briefing.Text);
        Assert.Equal(3, _agents.Calls.Count);
        Assert.Equal(3, _agents.Calls.Select(c => c.CorrelationId).Distinct().Count());
        Assert.All(_agents.Calls, c => Assert.Equal(_agents.Calls[0].Prompt, c.Prompt));
        using var scope = _services.CreateScope();
        var reads = await Db(scope).CaseReads.ToListAsync();
        Assert.Equal(_agents.Calls.Select(c => (Guid?)c.CorrelationId).Order(), reads.Select(r => r.CorrelationId).Order());
        Assert.All(reads, r => Assert.Equal((DraftRoleBriefing.AuditView, "role-briefing", "2"), (r.View, r.AgentName, r.AgentVersion)));
        Assert.Single(_logs.Lines, l => l.Contains(_agents.Calls[0].CorrelationId.ToString(), StringComparison.Ordinal) && l.EndsWith("(attempt 1 of 3): NOT_JSON", StringComparison.Ordinal));
        Assert.Single(_logs.Lines, l => l.Contains(_agents.Calls[1].CorrelationId.ToString(), StringComparison.Ordinal) && l.EndsWith("(attempt 2 of 3): MISSING_OUTPUT_FIELD", StringComparison.Ordinal));
        Assert.DoesNotContain(_logs.Lines, l => l.Contains("SYN", StringComparison.Ordinal));
    }

    [Fact]
    public async Task After_three_unusable_answers_each_endpoint_gives_the_calm_503()
    {
        var @case = await NamedCaseAsync();
        _agents.Answer = _ => "SYN prose, not JSON.";

        foreach (var result in new[] { await BriefingAsync(), await ImpactAsync(), await DraftAsync(@case.Id, Identity(DemoRoles.NurseNavigator)) })
        {
            Assert.Equal(503, Status(result));
            Assert.Equal("AI could not write this", Assert.IsType<ProblemDetails>(((IValueHttpResult)result).Value).Title);
        }

        Assert.Equal(9, _agents.Calls.Count);
        Assert.Equal(9, _logs.Lines.Count(l => l.EndsWith(": NOT_JSON", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_refusal_or_an_agent_that_cannot_be_reached_is_not_asked_again()
    {
        var @case = await NamedCaseAsync();
        _agents.Answer = _ => """{"cannot_comply":{"reason_code":"ROLE_SCOPE_CONFLICT","detail":"SYN"}}""";
        Assert.Equal(503, Status(await BriefingAsync()));
        Assert.Single(_agents.Calls);

        _agents.Answer = _ => "I'm unable to fulfill your request due to the following reason: SLOT_UNFILLED.";
        Assert.Equal(503, Status(await DraftAsync(@case.Id, Identity(DemoRoles.NurseNavigator))));
        Assert.Equal(2, _agents.Calls.Count);
        Assert.Single(_logs.Lines, l => l.EndsWith("(attempt 1 of 3): SLOT_UNFILLED", StringComparison.Ordinal));

        _agents.Failure = new AgentUnavailableException("role-briefing", "The agent answered HTTP 429.", 429);
        Assert.Equal(503, Status(await BriefingAsync()));
        Assert.Equal(3, _agents.Calls.Count);
    }

    [Fact]
    public async Task Every_attempt_together_stays_within_Ai_TimeoutSeconds()
    {
        await NamedCaseAsync();
        _ai.TimeoutSeconds = 1;
        _agents.Answers.Enqueue("SYN prose, not JSON.");
        _agents.Hang = true;

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = await BriefingAsync();

        Assert.Equal(503, Status(result));
        Assert.InRange(watch.Elapsed, TimeSpan.FromSeconds(0.9), TimeSpan.FromSeconds(10));
        Assert.Equal(2, _agents.Calls.Count);
        Assert.Contains("did not answer within 1 second.", Assert.IsType<ProblemDetails>(((IValueHttpResult)result).Value).Detail);
    }

    [Theory]
    [MemberData(nameof(EveryNextAction))]
    public void The_patient_message_prompt_never_carries_a_result_bearing_next_action(string nextAction)
    {
        var @case = NewCase("SYN-RESULT-STEP", "SYN-FAC-A");
        @case.SetNextAction(nextAction, _clock.UtcNow.AddDays(1), _clock.UtcNow);

        var prompt = DraftPatientMessage.BuildInput(@case, placeNamed: true, NotificationChannel.Sms, _format, _clock.UtcNow).Prompt;

        using var facts = AgentInput.Facts(prompt);
        var step = Assert.Single(facts.RootElement.GetProperty("facts").EnumerateArray(), f => f.GetProperty("fact_type").GetString() == "next_step")
            .GetProperty("value").GetString()!;
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
    public async Task The_briefing_sends_the_worklist_banded_and_ordered_overdue_or_escalated_then_due_before_the_next_working_day_then_urgent_then_the_next_working_days()
    {
        var late = await NamedCaseAsync();
        var urgent = await SeedAsync(NewCase("SYN-RESULT-URGENT", "SYN-FAC-A", urgency: "PLACEHOLDER-URGENCY-B", due: _clock.UtcNow.AddDays(5)));
        var today = await SeedAsync(NewCase("SYN-RESULT-TODAY", "SYN-FAC-A", due: _clock.UtcNow.AddHours(3)));
        var saturday = await SeedAsync(NewCase("SYN-RESULT-SATURDAY", "SYN-FAC-A", due: _clock.UtcNow.AddDays(1)));
        var later = await SeedAsync(NewCase("SYN-RESULT-LATER", "SYN-FAC-A", due: _clock.UtcNow.AddDays(3)));
        await SeedAsync(NewCase("SYN-RESULT-AFTER", "SYN-FAC-A", due: _clock.UtcNow.AddDays(30)));
        await SeedAsync(NewCase("SYN-RESULT-ELSEWHERE", "SYN-FAC-B", due: _clock.UtcNow.AddHours(1)));

        Assert.Equal(200, Status(await BriefingAsync()));

        var input = Facts(Assert.Single(_agents.Calls));
        Assert.Equal("clinician", input.GetProperty("role").GetString());
        Assert.Equal("SYN Family Health Centre, the reader's own cases", input.GetProperty("scope").GetString());
        Assert.Equal("2026-01-02T10:00+00:00", input.GetProperty("as_of").GetString());
        Assert.Equal(DraftRoleBriefing.WorkingDayCalendar, input.GetProperty("calendar_version").GetString());
        Assert.Equal("provisional", input.GetProperty("clock_config").GetProperty("status").GetString());
        Assert.Equal("""{"P1":1,"P2":2,"info":1,"P3":1}""", input.GetProperty("counts").GetRawText());
        var items = input.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["P1", "P2", "P2", "info", "P3"], items.Select(i => i.GetProperty("band").GetString()));
        Assert.Equal(["W-1", "W-2", "W-3", "W-4", "W-5"], items.Select(i => i.GetProperty("item_id").GetString()));
        Assert.Equal(["P-1", "P-2", "P-3", "P-4", "P-5"], items.Select(i => i.GetProperty("token").GetString()));
        Assert.Equal(new[] { late, today, saturday, urgent, later }.Select(c => AgentInput.Moment(_format.InLocalTime(c.DueAt!.Value))),
            items.Select(i => i.GetProperty("due_at").GetString()));
        Assert.Equal([0, 0, 3, 1], items.Skip(1).Select(i => i.GetProperty("working_days_remaining").GetInt32()));
        Assert.Equal(["routine", "not_reviewed", "not_reviewed", "urgent", "not_reviewed"], items.Select(i => i.GetProperty("urgency_tier").GetString()));
        Assert.Equal("Make sure the patient has the message", items[0].GetProperty("kind").GetString());
        Assert.False(items[0].TryGetProperty("label", out _));
        Assert.DoesNotContain("\"kind\":\"acknowledge-notification", Assert.Single(_agents.Calls).Prompt);
        var days = input.GetProperty("lookahead").EnumerateArray().ToList();
        Assert.Equal(["2026-01-05", "2026-01-06", "2026-01-07", "2026-01-08", "2026-01-09"], days.Select(d => d.GetProperty("date").GetString()));
        Assert.Equal("""["W-5"]""", days[0].GetProperty("item_ids").GetRawText());
        Assert.Equal(DraftRoleBriefing.MaxItemsPerSection, input.GetProperty("max_items_per_section").GetInt32());
    }

    [Fact]
    public void A_deadline_on_a_non_working_day_is_listed_under_the_working_day_before_it()
    {
        var monday = new DateTimeOffset(2026, 1, 5, 8, 0, 0, TimeSpan.Zero);
        var saturday = NewCase("SYN-RESULT-SATURDAY", "SYN-FAC-A", due: new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero));
        var reader = new CareLoop.Domain.Events.Actor { Reference = Reader, Role = DemoRoles.Clinician, FacilityId = "SYN-FAC-A" };

        var built = DraftRoleBriefing.BuildInput(reader, "SYN-FAC-A", false, [saturday], _pathways, _options.UrgentCodes, _format, monday);

        using var facts = AgentInput.Facts(built.Prompt);
        var item = Assert.Single(facts.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(("P3", 4), (item.GetProperty("band").GetString(), item.GetProperty("working_days_remaining").GetInt32()));
        var days = facts.RootElement.GetProperty("lookahead").EnumerateArray()
            .ToDictionary(d => d.GetProperty("date").GetString()!, d => d.GetProperty("item_ids").GetRawText());
        Assert.Equal(["2026-01-06", "2026-01-07", "2026-01-08", "2026-01-09", "2026-01-12"], days.Keys);
        Assert.Equal("""["W-1"]""", days["2026-01-09"]);
    }
    [Fact]
    public async Task The_briefing_describes_each_case_from_its_coordination_state_without_the_preferred_language()
    {
        var @case = await NamedCaseAsync(language: "ar-AE");
        await ChangeAsync(@case.Id, c =>
        {
            var notification = c.Notification ?? c.BeginNotification("patient-follow-up", _clock.UtcNow);
            notification.RecordAttempt(new NotificationAttempt
            {
                Id = Guid.NewGuid(), Channel = NotificationChannel.Sms, Status = NotificationAttemptStatus.Delivered, AttemptedAt = _clock.UtcNow,
                UsedSmsSafeTemplate = true,
            });
            notification.RecordCallRequest(_clock.UtcNow, null);
        });

        Assert.Equal(200, Status(await BriefingAsync(Identity(DemoRoles.NurseNavigator))));

        var call = Assert.Single(_agents.Calls);
        var item = Assert.Single(Facts(call).GetProperty("items").EnumerateArray());
        Assert.Equal("nurse", Facts(call).GetProperty("role").GetString());
        Assert.Equal("message delivered, no reply yet; the patient asked for a call", item.GetProperty("patient_contact").GetString());
        Assert.Equal("""{"level":1,"to":"facility lead","acknowledged":false}""", item.GetProperty("escalation").GetRawText());
        Assert.Equal("a clinician", item.GetProperty("acts_next").GetString());
        Assert.False(item.GetProperty("owner_is_user").GetBoolean());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("facility").ValueKind);
        Assert.DoesNotContain("Arabic", call.Prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ar-AE", call.Prompt, StringComparison.OrdinalIgnoreCase);
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

        var input = Facts(Assert.Single(_agents.Calls));
        Assert.Equal("programme_coordinator", input.GetProperty("role").GetString());
        Assert.Equal("every facility; only the cases escalated to the programme coordinator", input.GetProperty("scope").GetString());
        var item = Assert.Single(input.GetProperty("items").EnumerateArray());
        Assert.Equal("P1", item.GetProperty("band").GetString());
        Assert.Equal("SYN General Hospital", item.GetProperty("facility").GetString());
        Assert.Equal("""{"level":2,"to":"programme coordinator","acknowledged":false}""", item.GetProperty("escalation").GetRawText());
    }

    [Fact]
    public async Task The_briefing_covers_only_the_cases_the_dashboard_names_and_ignores_any_the_reader_may_not_read()
    {
        var mine = await SeedAsync(NewCase("SYN-RESULT-MINE-1", "SYN-FAC-A", due: _clock.UtcNow.AddHours(-1)));
        var alsoMine = await SeedAsync(NewCase("SYN-RESULT-MINE-2", "SYN-FAC-A", due: _clock.UtcNow.AddHours(2)));
        await SeedAsync(NewCase("SYN-RESULT-COLLEAGUE", "SYN-FAC-A", due: _clock.UtcNow.AddHours(-3)));
        var elsewhere = await SeedAsync(NewCase("SYN-RESULT-ELSEWHERE", "SYN-FAC-B", due: _clock.UtcNow.AddHours(-5)));

        Assert.Equal(200, Status(await BriefingAsync(caseIds: [mine.Id, alsoMine.Id, mine.Id, elsewhere.Id, Guid.NewGuid()])));

        var call = Assert.Single(_agents.Calls);
        var input = Facts(call);
        Assert.Equal("""{"P1":1,"P2":1,"info":0,"P3":0}""", input.GetProperty("counts").GetRawText());
        Assert.Equal(2, input.GetProperty("items").GetArrayLength());
        Assert.Equal("SYN Family Health Centre, the reader's own cases", input.GetProperty("scope").GetString());
        Assert.Contains(_logs.Lines, line => line.StartsWith("Role briefing ignored 2 of 4 named cases", StringComparison.Ordinal));
        Assert.DoesNotContain(mine.Id.ToString(), call.Prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_logs.Lines, line => line.Contains(elsewhere.Id.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(DraftRoleBriefing.MaxCaseIds + 1)]
    public async Task The_briefing_needs_from_1_to_1000_cases_named_and_asks_no_agent_otherwise(int count)
    {
        var result = await BriefingAsync(caseIds: Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList());

        Assert.Equal(400, Status(result));
        Assert.Empty(_agents.Calls);
    }

    [Fact]
    public async Task When_none_of_the_named_cases_is_open_and_the_readers_to_read_no_agent_is_asked_and_nothing_is_recorded()
    {
        var elsewhere = await SeedAsync(NewCase("SYN-RESULT-ELSEWHERE", "SYN-FAC-B", due: _clock.UtcNow.AddHours(-5)));
        var facilityLevel = await SeedAsync(NewCase("SYN-RESULT-LEAD", "SYN-FAC-A", due: _clock.UtcNow.AddHours(-2)));
        await ChangeAsync(facilityLevel.Id, c => c.Escalate(Escalation(1, DemoRoles.FacilityLead, "SYN-FAC-A")));

        Assert.Equal(400, Status(await BriefingAsync(caseIds: [elsewhere.Id, Guid.NewGuid()])));
        Assert.Equal(400, Status(await BriefingAsync(Identity(DemoRoles.Coordinator, facility: null), caseIds: [facilityLevel.Id])));

        Assert.Empty(_agents.Calls);
        using var scope = _services.CreateScope();
        Assert.Empty(await Db(scope).CaseReads.ToListAsync());
    }
    [Fact]
    public async Task The_briefing_says_which_cases_the_reader_acts_on_whether_or_not_the_header_names_a_FHIR_type()
    {
        var mine = await SeedAsync(NewCase("SYN-RESULT-MINE", "SYN-FAC-A", due: _clock.UtcNow.AddHours(-1)));
        await ChangeAsync(mine.Id, c => c.AssignOwner(TestData.Clinician(Reader), _clock.UtcNow));
        var context = Identity();
        context.Request.Headers[DemoRoles.UserHeader] = $"Practitioner/{Reader}";

        Assert.Equal(200, Status(await BriefingAsync(context)));

        var item = Assert.Single(Facts(_agents.Calls.Single()).GetProperty("items").EnumerateArray());
        Assert.True(item.GetProperty("owner_is_user").GetBoolean());
        Assert.Equal("the reader", item.GetProperty("acts_next").GetString());
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
    public async Task The_message_draft_fills_a_category_A_template_with_a_patient_safe_step_the_appointment_time_and_the_reply_codes_and_is_recorded_as_a_read()
    {
        var @case = await NamedCaseAsync();

        var draft = Value<AiTextResponse>(await DraftAsync(@case.Id, Identity(DemoRoles.NurseNavigator), NotificationChannel.Email));

        var call = Assert.Single(_agents.Calls);
        var input = Facts(call);
        Assert.Equal(("en", "email", 1000), (input.GetProperty("language").GetString(), input.GetProperty("channel").GetString(), input.GetProperty("max_chars").GetInt32()));
        var template = input.GetProperty("template");
        Assert.Equal(("A", "en"), (template.GetProperty("class").GetString(), template.GetProperty("language").GetString()));
        var spans = template.GetProperty("spans").EnumerateArray().ToList();
        Assert.Equal(("subject", "locked", DraftPatientMessage.EmailSubject), (spans[0].GetProperty("span_id").GetString(), spans[0].GetProperty("kind").GetString(), spans[0].GetProperty("text").GetString()));
        Assert.Contains(spans, s => s.GetProperty("kind").GetString() == "locked" && s.GetProperty("text").GetString() == DraftPatientMessage.ReplyCodes);
        Assert.Contains(spans, s => s.GetProperty("kind").GetString() == "locked" && s.GetProperty("text").GetString() == ". Place: {place}.");
        var facts = input.GetProperty("facts").EnumerateArray().ToDictionary(f => f.GetProperty("fact_type").GetString()!, f => f.GetProperty("value").GetString());
        Assert.Equal(new Dictionary<string, string?>
        {
            ["first_name"] = "{first name}", ["care_team"] = "SYN Family Health Centre",
            ["next_step"] = "Your care team sent you a message and would like to know that you got it.",
            ["appointment_time"] = "Sunday 4 January 2026 at 10:30",
        }, facts);
        Assert.All(input.GetProperty("facts").EnumerateArray(), f => Assert.Equal("final", f.GetProperty("review_status").GetString()));
        Assert.DoesNotContain("Make sure the patient has the message", call.Prompt);
        Assert.StartsWith($"{DraftPatientMessage.EmailSubject}\n\nHello {{first name}}, this is your care team at SYN Family Health Centre.", draft.Text);

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
    public async Task The_impact_prompt_holds_totals_only_with_small_numbers_suppressed_and_says_what_they_cover()
    {
        var @case = await NamedCaseAsync();

        Assert.Equal(200, Status(await ImpactAsync()));

        var call = Assert.Single(_agents.Calls);
        var input = Facts(call);
        Assert.Equal("""{"programme":"every programme","facility":"SYN Family Health Centre"}""", input.GetProperty("filters").GetRawText());
        Assert.Equal("2026-01-01", input.GetProperty("period").GetProperty("from").GetString());
        Assert.Equal("2026-01-02T10:00+00:00", input.GetProperty("period").GetProperty("to").GetString());
        Assert.Equal("metrics-v1", input.GetProperty("measure_definitions").GetString());
        Assert.True(input.GetProperty("synthetic").GetBoolean());
        Assert.Equal(JsonValueKind.Null, input.GetProperty("cohort_size").ValueKind);
        Assert.True(input.GetProperty("cohort_size_suppressed").GetBoolean());
        var overdue = Metric(input, "measures", "overdue_open");
        Assert.True(overdue.GetProperty("suppressed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, overdue.GetProperty("numerator").ValueKind);
        Assert.Equal("[]", input.GetProperty("targets").GetRawText());
        Assert.DoesNotContain(NotificationRouter.CaseReferenceFor(@case.Id), call.Prompt);
        Assert.DoesNotContain(@case.Id.ToString(), call.Prompt);
        Assert.DoesNotContain("going well", call.Prompt);
    }

    [Fact]
    public void The_impact_prompt_gives_counts_and_rates_from_5_and_suppresses_anything_smaller_on_either_side()
    {
        var metrics = new MetricsResponse
        {
            GeneratedAt = _clock.UtcNow, TotalCases = 40, OpenCases = 37, ClosedCases = 3,
            CasesByStage = new Dictionary<string, int>(), CasesByException = new Dictionary<string, int> { ["Overdue"] = 6, ["Unreachable"] = 2 },
            OverdueCases = 6, EscalatedCases = 1, UnassignedCases = 0,
            Measures = new Dictionary<string, RateMeasureResponse>
            {
                ["acknowledgement"] = new() { Numerator = 12, Denominator = 20 },
                ["closure"] = new() { Numerator = 3, Denominator = 40 },
                ["appointmentsAttended"] = new() { Numerator = 18, Denominator = 20 },
                ["understanding"] = new() { Numerator = 2, Denominator = 3 },
            },
            TimeToStep = new Dictionary<string, DurationMeasureResponse>
            {
                ["acknowledgement"] = new() { Denominator = 12, Median = TimeSpan.FromHours(5) },
                ["closure"] = new() { Denominator = 3, Median = TimeSpan.FromDays(2) },
            },
            ClosuresByReason = new Dictionary<string, int> { [nameof(ClosureReason.Declined)] = 1, [nameof(ClosureReason.Treated)] = 0 },
            CasesReachingDisplayLabel = new Dictionary<string, RateMeasureResponse>(),
            CasesByDisplayLabel = new Dictionary<string, int> { ["FollowUp"] = 30, ["Consultation"] = 4 },
            ConfidenceDenominator = 20, ConfidenceResponses = 3, AverageConfidence = 4.0,
        };

        var built = DraftImpactSummary.BuildInput(metrics, "SYN Family Health Centre", _format);

        using var document = AgentInput.Facts(built.Prompt);
        var input = document.RootElement;
        Assert.Equal(JsonValueKind.Null, input.GetProperty("cohort_size").ValueKind);
        Assert.Equal(JsonValueKind.Null, input.GetProperty("open_cases").ValueKind);
        Assert.True(input.GetProperty("cohort_size_suppressed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, input.GetProperty("period").GetProperty("from").ValueKind);
        Assert.Equal((6, 37, 16), Shown(Metric(input, "measures", "overdue_open")));
        Assert.Equal((0, 37, 0), Shown(Metric(input, "measures", "unassigned_open")));
        Assert.Equal((12, 20, 60), Shown(Metric(input, "measures", "acknowledgement")));
        Assert.Equal("patients told", Metric(input, "measures", "acknowledgement").GetProperty("denominator_label").GetString());
        foreach (var (group, id) in new[]
        {
            ("measures", "escalated_open"), ("measures", "closure"), ("measures", "appointmentsAttended"), ("measures", "understanding"),
            ("step_times", "time_to_closure"), ("delay_reasons", "waiting_Unreachable"), ("supplementary", "closed_Declined"),
            ("supplementary", "confidence_responses"),
        })
        {
            Assert.True(Metric(input, group, id).GetProperty("suppressed").GetBoolean(), id);
        }

        Assert.Equal("5 hours", Metric(input, "step_times", "time_to_acknowledgement").GetProperty("median").GetString());
        Assert.Equal(("past the deadline", 6), (Metric(input, "delay_reasons", "waiting_Overdue").GetProperty("label").GetString(),
            Metric(input, "delay_reasons", "waiting_Overdue").GetProperty("count").GetInt32()));
        Assert.Equal(6, input.GetProperty("delay_reasons_total").GetInt32());
        Assert.DoesNotContain(input.GetProperty("supplementary").EnumerateArray(), m => m.GetProperty("metric_id").GetString() == "confidence_average");
        Assert.Contains("overdue_open", built.MetricIds);
        Assert.Contains("time_to_acknowledgement", built.MetricIds);
        foreach (var hidden in new[] { "\"numerator\":1,", "\"numerator\":3,", "\"numerator\":18,", "\"numerator\":2,", "\"count\":2,", "\"count\":1,", "4.0", "\"value\":8,", "\"value\":90,", "\"value\":67," })
        {
            Assert.DoesNotContain(hidden, built.Prompt);
        }
    }
    [Fact]
    public void A_small_count_cannot_be_worked_out_from_the_totals_or_the_other_closure_reasons()
    {
        var metrics = new MetricsResponse
        {
            GeneratedAt = _clock.UtcNow, TotalCases = 40, OpenCases = 20, ClosedCases = 20,
            CasesByStage = new Dictionary<string, int>(), CasesByException = new Dictionary<string, int>(),
            OverdueCases = 5, EscalatedCases = 5, UnassignedCases = 5,
            Measures = new Dictionary<string, RateMeasureResponse>(), TimeToStep = new Dictionary<string, DurationMeasureResponse>(),
            ClosuresByReason = new Dictionary<string, int>
            {
                [nameof(ClosureReason.Treated)] = 10, [nameof(ClosureReason.OnSurveillance)] = 7, [nameof(ClosureReason.Declined)] = 3,
            },
            CasesReachingDisplayLabel = new Dictionary<string, RateMeasureResponse>(),
            CasesByDisplayLabel = new Dictionary<string, int>(),
            ConfidenceDenominator = 0, ConfidenceResponses = 0,
        };

        using var whole = AgentInput.Facts(DraftImpactSummary.BuildInput(metrics, "SYN Family Health Centre", _format).Prompt);
        using var fewOpen = AgentInput.Facts(DraftImpactSummary.BuildInput(metrics with { OpenCases = 2, TotalCases = 22 }, "SYN Family Health Centre", _format).Prompt);

        Assert.Equal((40, 20), (whole.RootElement.GetProperty("cohort_size").GetInt32(), whole.RootElement.GetProperty("open_cases").GetInt32()));
        Assert.Equal((10, 20, 50), Shown(Metric(whole.RootElement, "supplementary", $"closed_{nameof(ClosureReason.Treated)}")));
        Assert.True(Metric(whole.RootElement, "supplementary", $"closed_{nameof(ClosureReason.Declined)}").GetProperty("suppressed").GetBoolean());
        Assert.True(Metric(whole.RootElement, "supplementary", $"closed_{nameof(ClosureReason.OnSurveillance)}").GetProperty("suppressed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, fewOpen.RootElement.GetProperty("cohort_size").ValueKind);
        Assert.Equal(JsonValueKind.Null, fewOpen.RootElement.GetProperty("open_cases").ValueKind);
        Assert.Equal("[]", fewOpen.RootElement.GetProperty("funnel").GetRawText());
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
    private async Task<Case> NamedCaseAsync(string language = "en")
    {
        await IngestAsync(Envelopes.ScreeningResult(_clock.UtcNow, resultId: HospitalResultId, namedOwner: Envelopes.Clinician(),
            patientDisplayName: PatientName, patientSecondaryIdentifier: PatientMrn, patientLanguage: language));
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

    /// <summary>Asks for a briefing on the cases named, or on every case stored when none are named, as the dashboard would for a role whose own list is its team's.</summary>
    private async Task<IResult> BriefingAsync(HttpContext? context = null, string? facilityId = null, string? reason = null, IReadOnlyList<Guid>? caseIds = null)
    {
        using var scope = _services.CreateScope();
        var named = caseIds ?? await Db(scope).Cases.Select(c => c.Id).ToListAsync();
        return await DraftRoleBriefing.Handle(new RoleBriefingRequest { CaseIds = named }, facilityId, reason, context ?? Identity(), Db(scope),
            _pathways, _format, _agents, Options.Create(_options), Options.Create(_ai), _clock, _logs, CancellationToken.None);
    }

    private async Task<IResult> ImpactAsync(HttpContext? context = null, string? facilityId = null, string? reason = null)
    {
        using var scope = _services.CreateScope();
        return await DraftImpactSummary.Handle(facilityId, reason, context ?? Identity(), Db(scope), _pathways, _format, _agents,
            Options.Create(_options), Options.Create(_ai), _clock, _logs, CancellationToken.None);
    }

    private async Task<IResult> DraftAsync(Guid caseId, HttpContext context, NotificationChannel channel = NotificationChannel.Sms, string? reason = null)
    {
        using var scope = _services.CreateScope();
        return await DraftPatientMessage.Handle(caseId, context, Db(scope), _format, _agents, Options.Create(_options), Options.Create(_ai), _clock,
            _logs, CancellationToken.None, channel, reason);
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

    private static JsonElement Facts(AgentRequest call)
    {
        using var facts = AgentInput.Facts(call.Prompt);
        return facts.RootElement.Clone();
    }

    private static JsonElement Metric(JsonElement input, string group, string id) =>
        Assert.Single(input.GetProperty(group).EnumerateArray(), m => m.GetProperty("metric_id").GetString() == id);

    private static (int Numerator, int Denominator, int Value) Shown(JsonElement measure)
    {
        Assert.False(measure.GetProperty("suppressed").GetBoolean());
        return (measure.GetProperty("numerator").GetInt32(), measure.GetProperty("denominator").GetInt32(), measure.GetProperty("value").GetInt32());
    }

    /// <summary>
    /// An answer in the shape each agent gave live for the same input: a briefing listing every band's items with their
    /// tokens, one impact sentence citing a metric that was sent, and the patient template filled in exactly.
    /// </summary>
    private static string ValidReply(AgentRequest request)
    {
        var input = Facts(request);
        var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        return request.Agent.Name switch
        {
            "role-briefing" => JsonSerializer.Serialize(new
            {
                Headline = "SYN headline.",
                Sections = new { Today = Band(input, "P1"), ComingUp = Band(input, "P2"), Role = Band(input, "info"), Lookahead = Array.Empty<object>() },
            }, json),
            "impact-summary" => JsonSerializer.Serialize(new
            {
                Sentences = new[] { new { Text = "SYN sentence.", MetricIds = new[] { "overdue_open" } } },
                LimitsNote = "SYN limits note.",
            }, json),
            _ => JsonSerializer.Serialize(new
            {
                Status = "complied",
                Spans = input.GetProperty("template").GetProperty("spans").EnumerateArray().Select(span => new
                {
                    SpanId = span.GetProperty("span_id").GetString(),
                    Text = span.GetProperty("kind").GetString() == "slot"
                        ? input.GetProperty("facts").EnumerateArray()
                            .Single(f => f.GetProperty("fact_type").GetString() == span.GetProperty("slot_name").GetString()).GetProperty("value").GetString()
                        : span.GetProperty("text").GetString(),
                }).ToList(),
            }, json),
        };

        static object Band(JsonElement input, string band)
        {
            var items = input.GetProperty("items").EnumerateArray().Where(i => i.GetProperty("band").GetString() == band).ToList();
            return new
            {
                Intro = items.Count == 0 ? "None." : $"SYN {band}: {string.Join(", ", items.Select(i => i.GetProperty("token").GetString()))}.",
                ItemIds = items.Take(DraftRoleBriefing.MaxItemsPerSection).Select(i => i.GetProperty("item_id").GetString()).ToList(),
                OverflowCount = Math.Max(0, items.Count - DraftRoleBriefing.MaxItemsPerSection),
            };
        }
    }

    /// <summary>Keeps every log line, to check what reaches the log and what does not.</summary>
    private sealed class ListLoggerFactory : ILoggerFactory
    {
        public List<string> Lines { get; } = [];

        public ILogger CreateLogger(string categoryName) => new ListLogger(Lines);

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }

        private sealed class ListLogger(List<string> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                lines.Add(formatter(state, exception));
        }
    }
    private static CareLoopDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<CareLoopDbContext>();
    private static int Status(IResult result) => ((IStatusCodeHttpResult)result).StatusCode!.Value;
    private static T Value<T>(IResult result) => Assert.IsType<T>(((IValueHttpResult)result).Value);

    /// <summary>Stands in for the route <c>Ai:Provider</c> chooses: records every request and answers, or fails, as the test says.</summary>
    private sealed class FakeAgentClient : IAgentClient
    {
        public List<AgentRequest> Calls { get; } = [];

        public Func<AgentRequest, string> Answer { get; set; } = ValidReply;

        /// <summary>Answers given first, one per call, before <see cref="Answer"/> takes over.</summary>
        public Queue<string> Answers { get; } = new();

        /// <summary>When set, a call with no queued answer waits until it is cancelled, as an agent that never answers.</summary>
        public bool Hang { get; set; }

        public AgentUnavailableException? Failure { get; set; }

        public string Route => "Fake";

        public bool IsEnabled { get; set; } = true;

        public async Task<AgentText> AskAsync(AgentRequest request, CancellationToken ct)
        {
            Calls.Add(request);
            if (Failure is not null)
            {
                throw Failure;
            }

            if (Answers.TryDequeue(out var queued))
            {
                return new AgentText(queued, request.Agent);
            }

            if (Hang)
            {
                await Task.Delay(Timeout.Infinite, ct);
            }

            return new AgentText(Answer(request), request.Agent);
        }
    }
}

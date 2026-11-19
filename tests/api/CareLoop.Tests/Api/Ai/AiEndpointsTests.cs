using CareLoop.Api.Features.Ai;
using CareLoop.Api.Features.Events;
using CareLoop.Api.Features.Notifications;
using CareLoop.Api.Infrastructure;
using CareLoop.Api.Infrastructure.Ai;
using CareLoop.Api.Infrastructure.Auth;
using CareLoop.Api.Infrastructure.Notifications;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Api.Infrastructure.Persistence.Outbox;
using CareLoop.Contracts.Ai;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Recipients;
using CareLoop.Domain.Time;
using CareLoop.Tests.Domain;
using CareLoop.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CareLoop.Tests.Api.Ai;

/// <summary>
/// The three AI endpoints: what they send, what they withhold, when they reuse an answer and how they fail. A fake
/// stands in for the provider and records every prompt, so the tests read exactly what would leave CareLoop.
/// </summary>
public sealed class AiEndpointsTests : IDisposable
{
    private const string PatientName = "SYNTHETIC Hessa Example";
    private const string LatePatient = "SYNTHETIC Noura Example";
    private const string PatientMrn = "SYN-MRN-99887766";
    private const string PatientReference = "SYN-PT-0001";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly FakeClock _clock = new();
    private readonly ServiceProvider _services;
    private readonly IPathwayProvider _pathways = DomainTestData.Provider();
    private readonly FakeAiClient _ai = new();
    private readonly AiOptions _options = new();
    private readonly AiText _text;
    private readonly MessageFormat _format = new(new NotificationOptions
    {
        FacilityNames = new(StringComparer.OrdinalIgnoreCase) { ["SYN-FAC-A"] = "SYN Family Health Centre" },
    });

    public AiEndpointsTests()
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
        Db(scope).Database.EnsureCreated();
        _text = new AiText(_ai, TestPrompts.Shipped, new MemoryCache(new MemoryCacheOptions()), Options.Create(_options), _clock, NullLogger<AiText>.Instance);
    }

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Switched_off_every_endpoint_answers_409_and_nothing_is_read_or_sent()
    {
        var @case = await SeedAsync(NewCase("SYN-RESULT-1"));
        _ai.IsEnabled = false;

        foreach (var result in new[] { await BriefingAsync(), await ImpactAsync(), await DraftAsync(@case.Id) })
        {
            Assert.Equal(409, Status(result));
            Assert.Equal("AI is switched off", Value<ProblemDetails>(result).Title);
        }

        Assert.Empty(_ai.Prompts);
        using var scope = _services.CreateScope();
        Assert.Empty(await Db(scope).CaseReads.ToListAsync());
    }

    [Fact]
    public async Task The_briefing_sends_the_prompt_file_with_the_readers_counts_and_cases_and_returns_the_models_text()
    {
        await SeedAsync(NewCase("SYN-RESULT-1"));
        await SeedAsync(NewCase("SYN-RESULT-2", overdue: true, patient: LatePatient));
        _ai.Answer = "  A result review is waiting for {{C-1}}; C-2 is upcoming and {{C-9}} is unknown.  ";

        var response = Value<AiTextResponse>(await BriefingAsync());

        // The API writes the opening, and says who each case is where the model named it by its token (most pressing first).
        Assert.Equal($"Across your team's 2 open cases, 1 is overdue and 0 are due today. A result review is waiting for {LatePatient}; {PatientName} is upcoming and {{{{C-9}}}} is unknown.",
            response.Text);
        Assert.Equal(("Fake/role-briefing", _clock.UtcNow, false), (response.Model, response.GeneratedAt, response.Cached));
        var prompt = Assert.Single(_ai.Prompts);
        Assert.StartsWith("TASK: Summarize the reader's worklist.", prompt);
        Assert.Contains("\"overdue_by\": \"1 day 0 hours\"", prompt);
        Assert.Contains("\"opening\": \"Across your team's 2 open cases, 1 is overdue and 0 are due today.\"", prompt);
        Assert.Contains("\"token\": \"C-1\"", prompt);
        Assert.Equal(AiPrompts.RoleBriefing, Assert.Single(_ai.Requests).Use);
        using var scope = _services.CreateScope();
        Assert.Equal("role-briefing", Assert.Single(await Db(scope).CaseReads.ToListAsync()).View);
    }

    [Fact]
    public async Task No_prompt_carries_a_name_a_record_number_a_reference_or_a_case_or_staff_identifier()
    {
        var @case = await SeedAsync(NewCase("SYN-RESULT-1", overdue: true));

        await BriefingAsync();
        await ImpactAsync();
        await DraftAsync(@case.Id);
        await DraftAsync(@case.Id, channel: NotificationChannel.Email);

        Assert.Equal(4, _ai.Prompts.Count);
        Assert.All(_ai.Prompts, prompt =>
        {
            Assert.DoesNotContain(PatientName, prompt);
            Assert.DoesNotContain("Hessa", prompt);
            Assert.DoesNotContain("Noura", prompt);
            Assert.DoesNotContain(PatientMrn, prompt);
            Assert.DoesNotContain(PatientReference, prompt);
            Assert.DoesNotContain("SYN-RESULT-1", prompt);
            Assert.DoesNotContain(@case.Id.ToString(), prompt, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(@case.Id.ToString("N"), prompt, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(NotificationRouter.CaseReferenceFor(@case.Id), prompt, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(@case.Owner.Reference, prompt);
        });
    }

    [Fact]
    public async Task Unchanged_data_reuses_the_answer_and_refresh_or_a_change_asks_again()
    {
        var @case = await SeedAsync(NewCase("SYN-RESULT-1"));

        var first = Value<AiTextResponse>(await BriefingAsync());
        _clock.Advance(TimeSpan.FromMinutes(30));
        var again = Value<AiTextResponse>(await BriefingAsync());
        Assert.False(first.Cached);
        Assert.True(again.Cached);
        Assert.Equal((first.Text, first.GeneratedAt), (again.Text, again.GeneratedAt));
        Assert.Single(_ai.Prompts);

        var refreshed = Value<AiTextResponse>(await BriefingAsync(refresh: true));
        Assert.False(refreshed.Cached);
        Assert.True(refreshed.GeneratedAt > first.GeneratedAt);
        Assert.Equal(2, _ai.Prompts.Count);
        Assert.True(Value<AiTextResponse>(await BriefingAsync()).Cached);

        await ChangeAsync(@case.Id, c => c.FlagException(CaseExceptions.Overdue, _clock.UtcNow));
        Assert.False(Value<AiTextResponse>(await BriefingAsync()).Cached);
        Assert.Equal(3, _ai.Prompts.Count);

        _options.CacheHours = 0;
        Assert.False(Value<AiTextResponse>(await BriefingAsync()).Cached);
        Assert.Equal(4, _ai.Prompts.Count);
    }

    [Fact]
    public async Task A_failure_is_a_503_with_no_text_and_is_never_kept()
    {
        await SeedAsync(NewCase("SYN-RESULT-1"));
        _ai.Failure = new AiUnavailableException("SYN down");

        var failed = await ImpactAsync();
        Assert.Equal(503, Status(failed));
        Assert.Equal("AI could not write this", Value<ProblemDetails>(failed).Title);

        _ai.Failure = null;
        var written = Value<AiTextResponse>(await ImpactAsync());
        Assert.False(written.Cached);
        // The fixed sentence is the API's, added to whatever the model wrote, and only while the data is synthetic.
        Assert.Equal($"SYN summary.\n\n{ImpactSummary.SyntheticNote}", written.Text);
        _options.SyntheticData = false;
        Assert.Equal("SYN summary.", Value<AiTextResponse>(await ImpactAsync()).Text);
    }

    [Fact]
    public async Task A_model_that_does_not_answer_in_time_is_a_503()
    {
        await SeedAsync(NewCase("SYN-RESULT-1"));
        _options.TimeoutSeconds = 1;
        _ai.Hang = true;

        Assert.Equal(503, Status(await BriefingAsync()));
    }

    [Fact]
    public async Task The_impact_summary_withholds_small_counts_and_sends_larger_ones()
    {
        await SeedAsync(NewCase("SYN-RESULT-1", overdue: true));
        await ImpactAsync();
        var few = _ai.Prompts[^1];
        Assert.StartsWith("TASK: Generate the Impact summary", few);
        Assert.Contains("\"facility\": \"SYN Family Health Centre\"", few);
        Assert.Contains("\"open_cases\": \"withheld\"", few);
        Assert.Contains("\"open cases past their deadline\": \"withheld\"", few);
        Assert.DoesNotContain("\"open_cases\": 1", few);

        for (var i = 2; i <= 8; i++)
        {
            await SeedAsync(NewCase($"SYN-RESULT-{i}", overdue: true));
        }

        await ImpactAsync();
        var enough = _ai.Prompts[^1];
        Assert.Contains("\"open_cases\": 8", enough);
        using var data = System.Text.Json.JsonDocument.Parse(enough[(enough.IndexOf("DATA:", StringComparison.Ordinal) + 5)..]);
        var late = data.RootElement.GetProperty("shares").GetProperty("open cases past their deadline");
        Assert.Equal((8, 8, 100), (late.GetProperty("numerator").GetInt32(), late.GetProperty("denominator").GetInt32(), late.GetProperty("percent").GetInt32()));
        Assert.Equal(0, data.RootElement.GetProperty("shares").GetProperty("open cases without an owner").GetProperty("numerator").GetInt32());
    }

    [Fact]
    public async Task The_impact_summary_follows_the_metrics_access_rules_and_asks_nothing_when_refused()
    {
        await SeedAsync(NewCase("SYN-RESULT-1"));

        var refused = await ImpactAsync(facilityId: "SYN-FAC-B");

        Assert.Equal(403, Status(refused));
        Assert.Empty(_ai.Prompts);
    }

    [Fact]
    public void The_opening_names_its_scope_the_team_or_the_cases_escalated_to_the_coordinator()
    {
        var cases = new[] { NewCase("SYN-RESULT-1", overdue: true), NewCase("SYN-RESULT-2") };

        Assert.Equal("Across your team's 2 open cases, 1 is overdue and 0 are due today.", RoleBriefing.Opening(cases, _format, _clock.UtcNow));
        Assert.Equal("Of the 2 open cases escalated to you, 1 is overdue and 0 are due today.",
            RoleBriefing.Opening(cases, _format, _clock.UtcNow, escalatedToReader: true));
        Assert.Equal("Across your team's 1 open case, nothing is overdue or due today.", RoleBriefing.Opening(cases[1..], _format, _clock.UtcNow));
    }

    [Fact]
    public void A_case_number_that_starts_a_sentence_starts_with_a_capital()
    {
        // The programme coordinator sees case numbers, not names.
        var cases = new[] { NewCase("SYN-RESULT-1", overdue: true), NewCase("SYN-RESULT-2") };
        var coordinator = new CareLoop.Domain.Events.Actor { Reference = "SYN-STAFF-9", Role = DemoRoles.Coordinator };

        var text = RoleBriefing.WithCaseNames("{{C-1}} awaits a call. {{C-2}} is upcoming: C-1 first.", cases, coordinator);

        Assert.Equal($"Case {NotificationRouter.CaseReferenceFor(cases[0].Id)} awaits a call. Case {NotificationRouter.CaseReferenceFor(cases[1].Id)} is upcoming: "
            + $"case {NotificationRouter.CaseReferenceFor(cases[0].Id)} first.", text);
    }

    [Theory]
    [InlineData(50, "1 minute")]
    [InlineData(3660, "1 hour 1 minute")]
    [InlineData(183600, "2 days 3 hours")]
    public void A_median_time_is_sent_in_words_with_one_written_singular(int seconds, string words)
    {
        var empty = new Dictionary<string, int>();
        var metrics = new CareLoop.Contracts.Metrics.MetricsResponse
        {
            GeneratedAt = _clock.UtcNow, TotalCases = 0, OpenCases = 0, ClosedCases = 0, CasesByStage = empty, CasesByException = empty,
            OverdueCases = 0, EscalatedCases = 0, UnassignedCases = 0, Measures = new Dictionary<string, CareLoop.Contracts.Metrics.RateMeasureResponse>(),
            TimeToStep = new Dictionary<string, CareLoop.Contracts.Metrics.DurationMeasureResponse>
            {
                ["acknowledgement"] = new() { Denominator = 6, Median = TimeSpan.FromSeconds(seconds) },
            },
            ClosuresByReason = empty, CasesReachingDisplayLabel = new Dictionary<string, CareLoop.Contracts.Metrics.RateMeasureResponse>(),
            CasesByDisplayLabel = empty, ConfidenceDenominator = 0, ConfidenceResponses = 0,
        };

        var data = System.Text.Json.JsonSerializer.Serialize(ImpactSummary.BuildData(metrics, "SYN Family Health Centre", _format));

        Assert.Contains($"\"Time\":\"{words}\"", data);
    }

    [Fact]
    public void The_patient_message_draft_gives_the_facilitys_contact_when_one_is_configured()
    {
        var format = new MessageFormat(new NotificationOptions { FacilityContacts = new(StringComparer.OrdinalIgnoreCase) { ["SYN-FAC-A"] = "SYN-PHONE-A" } });

        var data = System.Text.Json.JsonSerializer.Serialize(PatientMessageDraft.BuildData(NewCase("SYN-RESULT-1"), NotificationChannel.Sms, format, DateTimeOffset.UnixEpoch));

        Assert.Contains("SYN-PHONE-A", data);
        Assert.DoesNotContain(PatientMessageDraft.ClinicContactPlaceholder, data);
    }

    [Theory]
    [InlineData("review-result")]
    [InlineData("start-treatment")]
    [InlineData("arrange-alternative-referral")]
    [InlineData("decide-closure")]
    public async Task The_patient_message_prompt_names_an_administrative_template_and_never_carries_the_next_action(string nextAction)
    {
        var @case = await SeedAsync(NewCase("SYN-RESULT-1", nextAction: nextAction));

        await DraftAsync(@case.Id);

        var prompt = Assert.Single(_ai.Prompts);
        Assert.StartsWith("TASK: Draft a patient notification", prompt);
        var data = prompt[prompt.IndexOf("DATA:", StringComparison.Ordinal)..];
        Assert.Contains("\"template\": \"T05\"", data);
        Assert.Contains("\"channel\": \"text message\"", data);
        Assert.Contains($"\"patient_name\": \"{PatientMessageDraft.FirstNamePlaceholder}\"", data);
        Assert.Contains("\"facility_name\": \"SYN Family Health Centre\"", data);
        Assert.Contains($"\"facility_contact\": \"{PatientMessageDraft.ClinicContactPlaceholder}\"", data);
        // A value there is nothing to say for is left out, not sent as null.
        Assert.DoesNotContain("appointment_date", data);
        Assert.DoesNotContain(nextAction, prompt);
        Assert.DoesNotContain(nextAction.Replace('-', ' '), prompt);
    }

    [Fact]
    public async Task The_patient_message_draft_gets_the_patients_first_name_only_after_the_model_has_written_it()
    {
        var @case = await SeedAsync(NewCase("SYN-RESULT-1", patient: "Hessa Al Dhaheri"));
        _ai.Answer = $"Dear {PatientMessageDraft.FirstNamePlaceholder},\nYour care team at SYN Family Health Centre would like to talk with you.";

        var draft = Value<AiTextResponse>(await DraftAsync(@case.Id, DemoRoles.NurseNavigator));

        Assert.StartsWith("Dear Hessa,\n", draft.Text);
        Assert.DoesNotContain("Al Dhaheri", draft.Text);
        Assert.DoesNotContain("Hessa", Assert.Single(_ai.Prompts));
    }

    [Fact]
    public void The_template_is_chosen_from_what_is_recorded_and_is_only_ever_an_administrative_one()
    {
        var plain = NewCase("SYN-RESULT-1");
        var unreachable = NewCase("SYN-RESULT-2");
        unreachable.FlagException(CaseExceptions.Unreachable, _clock.UtcNow);

        Assert.Equal("T05", PatientMessageDraft.TemplateFor(plain, _clock.UtcNow));
        Assert.Equal("T06", PatientMessageDraft.TemplateFor(unreachable, _clock.UtcNow));
        // Every template the API can name is in the prompt file, and none of them invites a patient to discuss a result.
        var prompt = TestPrompts.Shipped.Render(AiPrompts.PatientMessage, "{}");
        Assert.All(new[] { "T02", "T03", "T05", "T06" }, id => Assert.Contains($"\n{id} ", prompt));
        Assert.DoesNotContain("result", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_patient_message_draft_is_for_nurse_navigators_and_clinicians_is_audited_and_is_never_reused()
    {
        var @case = await SeedAsync(NewCase("SYN-RESULT-1", nextAction: "call-patient"));

        Assert.Equal(403, Status(await DraftAsync(@case.Id, DemoRoles.Coordinator, facility: null)));
        Assert.Equal(403, Status(await DraftAsync(@case.Id, DemoRoles.FacilityLead)));
        Assert.Equal(404, Status(await DraftAsync(Guid.NewGuid())));
        Assert.Empty(_ai.Prompts);

        var first = Value<AiTextResponse>(await DraftAsync(@case.Id, DemoRoles.NurseNavigator, channel: NotificationChannel.Email));
        var second = Value<AiTextResponse>(await DraftAsync(@case.Id, DemoRoles.NurseNavigator, channel: NotificationChannel.Email));

        Assert.False(first.Cached || second.Cached);
        Assert.Equal(2, _ai.Prompts.Count);
        Assert.Contains("\"channel\": \"email\"", _ai.Prompts[0]);
        using var scope = _services.CreateScope();
        Assert.Equal(2, await Db(scope).CaseReads.CountAsync(r => r.View == "patient-message-draft" && r.CaseId == @case.Id));
    }

    /// <summary>
    /// The whole path against the live Foundry agents: the real endpoints, the shipped prompt files and the real Foundry
    /// client with the signed-in identity, each use going to its own agent's latest version. Does nothing unless
    /// <c>CARELOOP_LIVE_FOUNDRY_ENDPOINT</c> names a project (after <c>az login</c>). Synthetic cases only.
    /// </summary>
    [Fact]
    public async Task Live_the_endpoints_get_text_from_their_foundry_agents()
    {
        if (Environment.GetEnvironmentVariable("CARELOOP_LIVE_FOUNDRY_ENDPOINT") is not { Length: > 0 } endpoint)
        {
            return;
        }

        var options = new AiOptions { Provider = "Foundry", TimeoutSeconds = 120, Foundry = new FoundryOptions { ProjectEndpoint = endpoint } };
        using var http = new HttpClient();
        var logs = new ListLogger();
        var live = new AiText(new FoundryAiClient(http, new Azure.Identity.DefaultAzureCredential(), Options.Create(options)), TestPrompts.Shipped,
            new MemoryCache(new MemoryCacheOptions()), Options.Create(options), _clock, logs);
        var called = await SeedAsync(NewCase("SYN-RESULT-0", overdue: true, nextAction: "call-patient"));
        for (var i = 1; i <= 11; i++)
        {
            await SeedAsync(NewCase($"SYN-RESULT-{i}", overdue: i % 2 == 0, nextAction: i % 3 == 0 ? "review-result" : "arrange-follow-up"));
        }

        using var scope = _services.CreateScope();
        var results = new (string Name, IResult Result)[]
        {
            ("role briefing", await RoleBriefing.Handle(Identity(), Db(scope), _pathways, _format, live, _clock, CancellationToken.None)),
            ("role briefing again", await RoleBriefing.Handle(Identity(), Db(scope), _pathways, _format, live, _clock, CancellationToken.None)),
            ("impact summary", await ImpactSummary.Handle(null, null, Identity(), Db(scope), _pathways, _format, live, Options.Create(options), _clock, NullLoggerFactory.Instance, CancellationToken.None)),
            ("patient text message", await PatientMessageDraft.Handle(called.Id, Identity(), Db(scope), _format, live, _clock, CancellationToken.None)),
            ("patient email", await PatientMessageDraft.Handle(called.Id, Identity(), Db(scope), _format, live, _clock, CancellationToken.None, NotificationChannel.Email)),
        };

        var report = string.Join("\n\n", results.Select(r => $"== {r.Name}: HTTP {Status(r.Result)}\n" + (((IValueHttpResult)r.Result).Value is AiTextResponse text
            ? $"[{text.Model}, cached {text.Cached}]\n{text.Text}"
            : (((IValueHttpResult)r.Result).Value as ProblemDetails)?.Title))) + "\n\n== log\n" + string.Join("\n", logs.Lines);
        if (Environment.GetEnvironmentVariable("CARELOOP_LIVE_FOUNDRY_REPORT") is { Length: > 0 } path)
        {
            await File.WriteAllTextAsync(path, report);
        }

        Assert.All(results, r => Assert.True(Status(r.Result) == 200, report));
        Assert.Equal("Foundry/patient-notification", Value<AiTextResponse>(results[3].Result).Model);
        Assert.StartsWith($"Dear {MessageFormat.FirstName(PatientName)},", Value<AiTextResponse>(results[3].Result).Text);
        Assert.EndsWith(ImpactSummary.SyntheticNote, Value<AiTextResponse>(results[2].Result).Text);
        Assert.True(Value<AiTextResponse>(results[1].Result).Cached);
        Assert.Equal("Foundry/role-briefing", Value<AiTextResponse>(results[0].Result).Model);
        Assert.Equal("Foundry/impact-summary", Value<AiTextResponse>(results[2].Result).Model);
        // The briefing names patients, because the API puts the names in after the answer; nothing else identifies anyone.
        Assert.Contains(PatientName, Value<AiTextResponse>(results[0].Result).Text);
        Assert.DoesNotContain(PatientMrn, report);
    }

    private Case NewCase(string result, bool overdue = false, string nextAction = "review-result", string patient = PatientName)
    {
        var @case = new Case(Guid.NewGuid(), "SYN-HIS", result, PatientReference, "breast-screening", "SYN-FAC-A",
            TestData.Clinician(), _clock.UtcNow, patient, PatientMrn);
        @case.MoveTo(CaseStage.ReviewPending, _clock.UtcNow);
        @case.SetNextAction(nextAction, _clock.UtcNow.AddDays(overdue ? -1 : 2), _clock.UtcNow);
        return @case;
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
        var @case = await Db(scope).Cases.SingleAsync(c => c.Id == id);
        change(@case);
        @case.ClearDomainEvents();
        await Db(scope).SaveChangesAsync();
    }

    private async Task<IResult> BriefingAsync(bool refresh = false)
    {
        using var scope = _services.CreateScope();
        return await RoleBriefing.Handle(Identity(), Db(scope), _pathways, _format, _text, _clock, CancellationToken.None, refresh);
    }

    private async Task<IResult> ImpactAsync(string? facilityId = null, bool refresh = false)
    {
        using var scope = _services.CreateScope();
        return await ImpactSummary.Handle(facilityId, null, Identity(), Db(scope), _pathways, _format, _text, Options.Create(_options), _clock,
            NullLoggerFactory.Instance, CancellationToken.None, refresh);
    }

    private async Task<IResult> DraftAsync(Guid caseId, string role = DemoRoles.Clinician, string? facility = "SYN-FAC-A",
        NotificationChannel channel = NotificationChannel.Sms)
    {
        using var scope = _services.CreateScope();
        return await PatientMessageDraft.Handle(caseId, Identity(role, facility), Db(scope), _format, _text, _clock, CancellationToken.None, channel);
    }

    private static DefaultHttpContext Identity(string role = DemoRoles.Clinician, string? facility = "SYN-FAC-A")
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[DemoRoles.UserHeader] = "SYN-STAFF-9";
        context.Request.Headers[DemoRoles.RoleHeader] = role;
        if (facility is not null) context.Request.Headers[DemoRoles.FacilityHeader] = facility;
        return context;
    }

    private static CareLoopDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<CareLoopDbContext>();

    private static int Status(IResult result) => ((IStatusCodeHttpResult)result).StatusCode!.Value;

    private static T Value<T>(IResult result) => Assert.IsType<T>(((IValueHttpResult)result).Value);

    /// <summary>Stands in for the AI provider: records every prompt and answers, fails or hangs as the test says.</summary>
    private sealed class FakeAiClient : IAiClient
    {
        public List<string> Prompts { get; } = [];

        public string Answer { get; set; } = "SYN summary.";

        public AiUnavailableException? Failure { get; set; }

        public bool Hang { get; set; }

        public List<AiRequest> Requests { get; } = [];

        public bool IsEnabled { get; set; } = true;

        public string Describe(string use) => $"Fake/{use}";

        public async Task<string> CompleteAsync(AiRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            Prompts.Add(request.Prompt);
            if (Failure is not null) throw Failure;
            if (Hang) await Task.Delay(Timeout.Infinite, ct);
            return Answer;
        }
    }

    private sealed class ListLogger : Microsoft.Extensions.Logging.ILogger<AiText>
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) => Lines.Add(formatter(state, exception));
    }

    private sealed class UtcModelCustomizer(ModelCustomizerDependencies dependencies) : ModelCustomizer(dependencies)
    {
        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            base.Customize(modelBuilder, context);
            foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetProperties())
                .Where(p => (Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType) == typeof(DateTimeOffset)))
            {
                property.SetValueConverter(new ValueConverter<DateTimeOffset, long>(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero)));
            }
        }
    }
}

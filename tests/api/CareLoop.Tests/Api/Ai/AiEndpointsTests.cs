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
        await SeedAsync(NewCase("SYN-RESULT-2", overdue: true));
        _ai.Answer = "  2 cases are open; 1 is overdue.  ";

        var response = Value<AiTextResponse>(await BriefingAsync());

        Assert.Equal(("2 cases are open; 1 is overdue.", "Fake/model", _clock.UtcNow, false), (response.Text, response.Model, response.GeneratedAt, response.Cached));
        var prompt = Assert.Single(_ai.Prompts);
        Assert.StartsWith("You are writing the short briefing", prompt);
        Assert.Contains("\"reader_role\": \"clinician\"", prompt);
        Assert.Contains("\"open_cases\": 2", prompt);
        Assert.Contains("\"overdue\": 1", prompt);
        Assert.Contains("\"next_action\": \"review result\"", prompt);
        Assert.Contains("\"status\": \"overdue\"", prompt);
        Assert.DoesNotContain(AiPrompts.DataMarker, prompt);
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
        Assert.False(Value<AiTextResponse>(await ImpactAsync()).Cached);
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
        Assert.StartsWith("You are writing the short summary", few);
        Assert.Contains("\"facility\": \"SYN Family Health Centre\"", few);
        Assert.Contains("\"open_cases\": \"withheld: fewer than 5\"", few);
        Assert.Contains("\"open_cases_past_deadline\": \"withheld: fewer than 5\"", few);
        Assert.DoesNotContain("\"open_cases\": 1", few);

        for (var i = 2; i <= 8; i++)
        {
            await SeedAsync(NewCase($"SYN-RESULT-{i}", overdue: true));
        }

        await ImpactAsync();
        var enough = _ai.Prompts[^1];
        Assert.Contains("\"open_cases\": 8", enough);
        Assert.Contains("\"open_cases_past_deadline\": \"8 of 8 (100%)\"", enough);
        Assert.Contains("\"open_cases_without_owner\": \"0 of 8 (0%)\"", enough);
    }

    [Fact]
    public async Task The_impact_summary_follows_the_metrics_access_rules_and_asks_nothing_when_refused()
    {
        await SeedAsync(NewCase("SYN-RESULT-1"));

        var refused = await ImpactAsync(facilityId: "SYN-FAC-B");

        Assert.Equal(403, Status(refused));
        Assert.Empty(_ai.Prompts);
    }

    [Theory]
    [InlineData("review-result")]
    [InlineData("start-treatment")]
    [InlineData("arrange-alternative-referral")]
    [InlineData("decide-closure")]
    public async Task The_patient_message_prompt_never_carries_a_next_action_that_could_reveal_a_result(string nextAction)
    {
        var @case = await SeedAsync(NewCase("SYN-RESULT-1", nextAction: nextAction));

        await DraftAsync(@case.Id);

        var prompt = Assert.Single(_ai.Prompts);
        Assert.StartsWith("You are drafting a message", prompt);
        Assert.Contains($"\"purpose\": \"{PatientMessageDraft.NeutralPurpose}\"", prompt);
        Assert.Contains("\"clinic\": \"SYN Family Health Centre\"", prompt);
        Assert.Contains("\"channel\": \"text message\"", prompt);
        Assert.Contains("\"max_characters\": 459", prompt);
        var data = prompt[prompt.IndexOf("Data:", StringComparison.Ordinal)..];
        Assert.DoesNotContain(nextAction, data);
        Assert.DoesNotContain(nextAction.Replace('-', ' '), data);
    }

    [Fact]
    public void Every_pathway_next_action_maps_to_a_purpose_with_no_result_in_it()
    {
        var actions = _pathways.GetAll().SelectMany(p => p.Deadlines.Select(d => d.NextAction)).Distinct().Append(null).Append("unknown");

        Assert.All(actions, action =>
        {
            var purpose = PatientMessageDraft.PurposeFor(action);
            Assert.DoesNotContain("result", purpose, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("treatment", purpose, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("referral", purpose, StringComparison.OrdinalIgnoreCase);
        });
        Assert.NotEqual(PatientMessageDraft.NeutralPurpose, PatientMessageDraft.PurposeFor("call-patient"));
        Assert.Equal(PatientMessageDraft.NeutralPurpose, PatientMessageDraft.PurposeFor("review-result"));
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
        Assert.Contains("\"max_characters\": 1000", _ai.Prompts[0]);
        Assert.Contains("will call the patient back", _ai.Prompts[0]);
        using var scope = _services.CreateScope();
        Assert.Equal(2, await Db(scope).CaseReads.CountAsync(r => r.View == "patient-message-draft" && r.CaseId == @case.Id));
    }

    /// <summary>
    /// The whole path against a live model: the real endpoints, the shipped prompt files and the real Foundry client with
    /// the signed-in identity. Skipped unless <c>CARELOOP_LIVE_FOUNDRY_ENDPOINT</c> names a project (after <c>az login</c>);
    /// <c>CARELOOP_LIVE_FOUNDRY_MODEL</c> names the model (default <c>gpt-6.1-sol</c>). Synthetic cases only.
    /// </summary>
    [Fact]
    public async Task Live_the_three_endpoints_get_text_from_the_foundry_model()
    {
        if (Environment.GetEnvironmentVariable("CARELOOP_LIVE_FOUNDRY_ENDPOINT") is not { Length: > 0 } endpoint)
        {
            return;
        }

        var options = new AiOptions
        {
            Provider = "Foundry", TimeoutSeconds = 90,
            Foundry = new FoundryOptions { ProjectEndpoint = endpoint, Model = Environment.GetEnvironmentVariable("CARELOOP_LIVE_FOUNDRY_MODEL") ?? "gpt-6.1-sol" },
        };
        using var http = new HttpClient();
        var live = new AiText(new FoundryAiClient(http, new Azure.Identity.DefaultAzureCredential(), Options.Create(options)), TestPrompts.Shipped,
            new MemoryCache(new MemoryCacheOptions()), Options.Create(options), _clock, NullLogger<AiText>.Instance);
        var called = await SeedAsync(NewCase("SYN-RESULT-0", overdue: true, nextAction: "call-patient"));
        for (var i = 1; i <= 9; i++)
        {
            await SeedAsync(NewCase($"SYN-RESULT-{i}", overdue: i % 3 == 0, nextAction: i % 2 == 0 ? "review-result" : "arrange-follow-up"));
        }

        using var scope = _services.CreateScope();
        var results = new (string Name, IResult Result)[]
        {
            ("role briefing", await RoleBriefing.Handle(Identity(), Db(scope), _pathways, _format, live, _clock, CancellationToken.None)),
            ("role briefing again", await RoleBriefing.Handle(Identity(), Db(scope), _pathways, _format, live, _clock, CancellationToken.None)),
            ("impact summary", await ImpactSummary.Handle(null, null, Identity(), Db(scope), _pathways, _format, live, _clock, NullLoggerFactory.Instance, CancellationToken.None)),
            ("patient text message", await PatientMessageDraft.Handle(called.Id, Identity(), Db(scope), _format, live, _clock, CancellationToken.None)),
            ("patient email", await PatientMessageDraft.Handle(called.Id, Identity(), Db(scope), _format, live, _clock, CancellationToken.None, NotificationChannel.Email)),
        };

        var report = string.Join("\n\n", results.Select(r => $"== {r.Name}: HTTP {Status(r.Result)}\n" + (((IValueHttpResult)r.Result).Value is AiTextResponse text
            ? $"[{text.Model}, cached {text.Cached}]\n{text.Text}"
            : (((IValueHttpResult)r.Result).Value as ProblemDetails)?.Title)));
        if (Environment.GetEnvironmentVariable("CARELOOP_LIVE_FOUNDRY_REPORT") is { Length: > 0 } path)
        {
            await File.WriteAllTextAsync(path, report);
        }

        Assert.All(results, r => Assert.True(Status(r.Result) == 200, report));
        Assert.True(Value<AiTextResponse>(results[1].Result).Cached);
        Assert.DoesNotContain("Hessa", report);
        Assert.DoesNotContain(PatientMrn, report);
    }

    private Case NewCase(string result, bool overdue = false, string nextAction = "review-result")
    {
        var @case = new Case(Guid.NewGuid(), "SYN-HIS", result, PatientReference, "breast-screening", "SYN-FAC-A",
            TestData.Clinician(), _clock.UtcNow, PatientName, PatientMrn);
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
        return await ImpactSummary.Handle(facilityId, null, Identity(), Db(scope), _pathways, _format, _text, _clock, NullLoggerFactory.Instance,
            CancellationToken.None, refresh);
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

        public string Model => "Fake/model";

        public bool IsEnabled { get; set; } = true;

        public async Task<string> CompleteAsync(string prompt, CancellationToken ct)
        {
            Prompts.Add(prompt);
            if (Failure is not null) throw Failure;
            if (Hang) await Task.Delay(Timeout.Infinite, ct);
            return Answer;
        }
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

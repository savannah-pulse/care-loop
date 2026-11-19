using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Core;
using CareLoop.Api.Features.Ai;
using CareLoop.Api.Infrastructure;
using CareLoop.Api.Infrastructure.Ai;
using CareLoop.Tests.Fakes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CareLoop.Tests.Api.Ai;

/// <summary>
/// The provider side: the prompt files, which client <c>Ai:Provider</c> chooses, and what the Foundry client sends.
/// </summary>
public sealed class AiProviderTests
{
    private const string Endpoint = "https://syn-foundry.invalid/api/projects/syn";

    [Fact]
    public void The_shipped_prompt_files_load_and_put_the_data_where_the_marker_is()
    {
        var prompt = TestPrompts.Shipped.Render(AiPrompts.ImpactSummary, "{\"open_cases\": 8}");

        Assert.StartsWith("You are writing the short summary", prompt);
        Assert.Contains("Data:\n{\"open_cases\": 8}", prompt);
        Assert.DoesNotContain(AiPrompts.DataMarker, prompt);
        Assert.All(new[] { AiPrompts.RoleBriefing, AiPrompts.PatientMessage }, name => Assert.Contains("SYN-DATA", TestPrompts.Shipped.Render(name, "SYN-DATA")));
    }

    [Fact]
    public void An_edited_prompt_file_changes_what_is_sent_with_no_code_change()
    {
        var directory = CopyOfShippedPrompts();
        File.WriteAllText(Path.Combine(directory, "role-briefing.md"), "SYN new instructions.\n{{data}}\nSYN after.");

        Assert.Equal("SYN new instructions.\n{}\nSYN after.", new AiPrompts(directory).Render(AiPrompts.RoleBriefing, "{}"));
    }

    [Theory]
    [InlineData("No marker here.")]
    [InlineData("{{data}} twice {{data}}")]
    public void A_prompt_file_without_exactly_one_data_marker_stops_the_host(string text)
    {
        var directory = CopyOfShippedPrompts();
        File.WriteAllText(Path.Combine(directory, "patient-message.md"), text);

        Assert.Contains("patient-message.md", Assert.Throws<InvalidOperationException>(() => new AiPrompts(directory)).Message);
    }

    [Fact]
    public void A_missing_prompt_file_stops_the_host()
    {
        var directory = CopyOfShippedPrompts();
        File.Delete(Path.Combine(directory, "impact-summary.md"));

        Assert.Contains("impact-summary.md", Assert.Throws<InvalidOperationException>(() => new AiPrompts(directory)).Message);
    }

    [Theory]
    [InlineData(null, null, typeof(NoOpAiClient))]
    [InlineData("NoOp", null, typeof(NoOpAiClient))]
    [InlineData("Foundry", Endpoint, typeof(FoundryAiClient))]
    [InlineData("foundry", Endpoint, typeof(FoundryAiClient))]
    public void The_provider_setting_chooses_the_client(string? provider, string? endpoint, Type client)
    {
        using var services = Services(provider, endpoint).BuildServiceProvider();

        Assert.IsType(client, services.GetRequiredService<IAiClient>());
        Assert.Equal(provider is null or "NoOp" ? "NoOp" : "Foundry/role-briefing", services.GetRequiredService<IAiClient>().Describe(AiPrompts.RoleBriefing));
        Assert.Equal(provider is null or "NoOp" ? "NoOp" : "Foundry/syn-patient-agent", services.GetRequiredService<IAiClient>().Describe(AiPrompts.PatientMessage));
    }

    [Theory]
    [InlineData("Foundry", null)]
    [InlineData("Foundry", "http://syn-foundry.invalid/api/projects/syn")]
    [InlineData("AiWorkflow", null)]
    [InlineData("FoundryDirect", Endpoint)]
    [InlineData("AzureOpenAi", Endpoint)]
    public void A_provider_that_cannot_run_stops_the_host(string provider, string? endpoint) =>
        Assert.Throws<InvalidOperationException>(() => Services(provider, endpoint));

    [Fact]
    public void Base_settings_keep_ai_off_with_no_endpoint_and_no_key()
    {
        var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json")).Build();
        var ai = configuration.GetSection(AiOptions.SectionName).Get<AiOptions>()!;

        Assert.Equal("NoOp", ai.Provider);
        Assert.True(string.IsNullOrEmpty(ai.Foundry.ProjectEndpoint));
        Assert.Equal(6, ai.CacheHours);
        Assert.DoesNotContain(configuration.GetSection(AiOptions.SectionName).AsEnumerable(), s => s.Key.Contains("Key", StringComparison.OrdinalIgnoreCase));
    }

    private static readonly AiRequest Briefing = new(AiPrompts.RoleBriefing, "SYN task", """{"opening":"SYN opening."}""", "SYN task\n{\"opening\":\"SYN opening.\"}");

    [Fact]
    public async Task The_foundry_client_asks_the_uses_agent_by_name_with_a_bearer_token_and_reads_the_text()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            """{"status":"completed","output":[{"type":"reasoning"},{"type":"message","content":[{"type":"output_text","text":"SYN continuation."}]}]}"""));
        var credential = new CountingCredential();
        var client = Client(handler, credential);

        var text = await client.CompleteAsync(Briefing, CancellationToken.None);
        await client.CompleteAsync(Briefing, CancellationToken.None);

        Assert.Equal("SYN continuation.", text);
        var sent = handler.Requests[0];
        Assert.Equal((HttpMethod.Post, $"{Endpoint}/openai/v1/responses", "Bearer SYN-TOKEN"), (sent.Method, sent.Uri, sent.Authorization));
        using var body = JsonDocument.Parse(sent.Body);
        var agent = body.RootElement.GetProperty("agent_reference");
        Assert.Equal(("agent_reference", "role-briefing"), (agent.GetProperty("type").GetString(), agent.GetProperty("name").GetString()));
        // No version: the agent's latest version answers.
        Assert.False(agent.TryGetProperty("version", out _));
        Assert.False(body.RootElement.TryGetProperty("model", out _));
        using var input = JsonDocument.Parse(body.RootElement.GetProperty("input")[0].GetProperty("content").GetString()!);
        Assert.Equal(("SYN task", "SYN opening."), (input.RootElement.GetProperty("task").GetString(), input.RootElement.GetProperty("data").GetProperty("opening").GetString()));
        Assert.Equal(1, credential.Calls);
        Assert.Equal(["https://ai.azure.com/.default"], credential.Scopes);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, """{"error":{"code":"PermissionDenied"}}""")]
    [InlineData(HttpStatusCode.NotFound, """{"error":{"code":"DeploymentNotFound"}}""")]
    [InlineData(HttpStatusCode.TooManyRequests, "{}")]
    [InlineData(HttpStatusCode.OK, """{"status":"completed","output":[]}""")]
    [InlineData(HttpStatusCode.OK, """{"status":"completed","output":[{"content":[{"text":"CANNOT_COMPLY:MISSING_FIELD"}]}]}""")]
    [InlineData(HttpStatusCode.OK, "SYN not json")]
    public async Task The_foundry_client_fails_closed_on_an_error_a_refusal_or_an_answer_with_no_text(HttpStatusCode status, string body) =>
        await Assert.ThrowsAsync<AiUnavailableException>(() =>
            Client(new StubHandler(_ => Json(status, body)), new CountingCredential()).CompleteAsync(Briefing, CancellationToken.None));

    [Fact]
    public void The_impact_agent_is_sent_its_structured_input_with_withheld_values_suppressed_and_no_number_for_them()
    {
        const string data = """
            {"facility":"SYN Family Health Centre","period":"Cases opened up to Thursday 1 January 2026","total_cases":40,"open_cases":28,"closed_cases":12,
             "shares":{"open cases past their deadline":{"numerator":9,"denominator":28,"percent":32},"acknowledgement":"withheld"},
             "median_time_from_result_to":{"consultation":{"time":"2 days 3 hours","cases":18},"closure":"withheld"},
             "open_cases_waiting_because":{"unreachable":6,"overdue":9,"rejected":"withheld"},
             "closed_cases_by_reason":{"treated":7,"declined":5}}
            """;

        var message = FoundryAgentFormat.ToAgentInput(new AiRequest(AiPrompts.ImpactSummary, "SYN task", data, "SYN prompt"), synthetic: true);

        Assert.StartsWith("<input_json>\n", message);
        Assert.EndsWith("\n</input_json>", message);
        using var input = JsonDocument.Parse(message["<input_json>\n".Length..^"\n</input_json>".Length]);
        var root = input.RootElement;
        Assert.Equal(("Cases opened up to Thursday 1 January 2026", "SYN Family Health Centre", 40, true),
            (root.GetProperty("period").GetProperty("label").GetString(), root.GetProperty("filters").GetProperty("facility_label").GetString(),
                root.GetProperty("cohort_size").GetInt32(), root.GetProperty("synthetic").GetBoolean()));
        var measures = root.GetProperty("measures").EnumerateArray().ToList();
        Assert.Equal((9, 28, 32, false), (measures[0].GetProperty("numerator").GetInt32(), measures[0].GetProperty("denominator").GetInt32(),
            measures[0].GetProperty("value").GetInt32(), measures[0].GetProperty("suppressed").GetBoolean()));
        Assert.True(measures[1].GetProperty("suppressed").GetBoolean());
        Assert.False(measures[1].TryGetProperty("numerator", out _));
        // Reasons in descending order, the withheld one left out, and the total is of what was sent.
        Assert.Equal(["overdue", "unreachable"], root.GetProperty("delay_reasons").EnumerateArray().Select(r => r.GetProperty("label").GetString()));
        Assert.Equal(15, root.GetProperty("delay_reasons_total").GetInt32());
        Assert.Equal(4, root.GetProperty("supplementary").GetArrayLength());
        Assert.DoesNotContain("SYN task", message);
    }

    [Theory]
    [InlineData("""{"agent":"AG3","status":"ready","paragraphs":[{"sentences":[{"text":"SYN one.","metric_ids":["a"]},{"text":"SYN two.","metric_ids":[]}]},{"sentences":[{"text":"SYN three.","metric_ids":[]}]}],"limits_note":"SYN limits."}""")]
    [InlineData("```json\n{\"status\":\"ready\",\"paragraphs\":[{\"sentences\":[{\"text\":\"SYN one.\"},{\"text\":\"SYN two.\"}]},{\"sentences\":[{\"text\":\"SYN three.\"}]}],\"limits_note\":\"SYN limits.\"}\n```")]
    public void The_impact_agents_answer_is_read_as_paragraphs_and_the_limits_note(string answer) =>
        Assert.Equal("SYN one. SYN two.\n\nSYN three.\n\nSYN limits.", FoundryAgentFormat.ToText(AiPrompts.ImpactSummary, answer));

    [Theory]
    [InlineData(AiPrompts.ImpactSummary, """{"agent":"AG3","status":"cannot_comply","paragraphs":[],"limits_note":"","reason_code":"ALL_MEASURES_SUPPRESSED"}""")]
    [InlineData(AiPrompts.ImpactSummary, "SYN plain words, not the agent's JSON")]
    [InlineData(AiPrompts.ImpactSummary, """{"status":"ready"}""")]
    [InlineData(AiPrompts.RoleBriefing, "CANNOT_COMPLY:OTHER")]
    [InlineData(AiPrompts.RoleBriefing, "  ")]
    public void A_refusal_or_an_answer_in_another_shape_is_a_failure_never_shown_as_text(string use, string answer) =>
        Assert.Throws<AiUnavailableException>(() => FoundryAgentFormat.ToText(use, answer));

    [Theory]
    [InlineData("please contact the care team", "T05", "callback_task_verified", "true", 6)]
    [InlineData("could not reach the patient", "T06", "contact_attempt_status", "unsuccessful", 6)]
    [InlineData("appointment reminder", "T02", "appointment_status", "confirmed", 8)]
    public void The_patient_agent_is_told_which_of_its_templates_to_fill_and_given_placeholders_not_the_patient(
        string situation, string template, string condition, string value, int factCount)
    {
        var data = $$"""
            {"channel":"text message","max_characters":459,"clinic":"SYN Family Health Centre","situation":"{{situation}}",
             "purpose":"SYN purpose","appointment_date":"Sunday 4 January 2026","appointment_time":"10:30"}
            """;

        var message = FoundryAgentFormat.ToAgentInput(new AiRequest(AiPrompts.PatientMessage, "SYN task", data, "SYN prompt"), synthetic: true);

        using var input = JsonDocument.Parse(message["<input_json>\n".Length..^"\n</input_json>".Length]);
        var root = input.RootElement;
        Assert.Equal((template, "approved", "sms", 459, "en"), (root.GetProperty("template").GetProperty("template_id").GetString(),
            root.GetProperty("template").GetProperty("approval_status").GetString(), root.GetProperty("channel").GetString(),
            root.GetProperty("max_chars").GetInt32(), root.GetProperty("language").GetString()));
        var facts = root.GetProperty("facts").EnumerateArray().ToDictionary(f => f.GetProperty("fact_type").GetString()!, f => f.GetProperty("value").GetString());
        Assert.Equal(factCount, facts.Count);
        Assert.Equal((FoundryAgentFormat.FirstNamePlaceholder, "SYN Family Health Centre", FoundryAgentFormat.ClinicContactPlaceholder, value),
            (facts["patient_name"], facts["facility_name"], facts["facility_contact"], facts[condition]));
        Assert.Equal(template == "T02", facts.ContainsKey("appointment_date"));
        Assert.All(root.GetProperty("facts").EnumerateArray(), f => Assert.Equal("final", f.GetProperty("review_status").GetString()));
        // Only templates with no result content are ever named, and the free wording is not sent.
        Assert.DoesNotContain("SYN purpose", message);
        Assert.DoesNotContain("SYN task", message);
    }

    [Fact]
    public void The_patient_agents_draft_is_read_from_its_answer_and_a_refusal_is_a_failure()
    {
        const string draft = """{"agent":"AG2","status":"draft","template_id":"T05","lines":[],"draft_text":"Dear {first name},\nSYN line.\nThank you.","requires_human_approval":true}""";
        const string refused = """{"agent":"AG2","status":"cannot_comply","lines":[],"draft_text":"","reason_code":"SENSITIVE_SLOT_VALUE"}""";

        Assert.Equal("Dear {first name},\nSYN line.\nThank you.", FoundryAgentFormat.ToText(AiPrompts.PatientMessage, draft));
        Assert.Contains("SENSITIVE_SLOT_VALUE", Assert.Throws<AiUnavailableException>(() => FoundryAgentFormat.ToText(AiPrompts.PatientMessage, refused)).Message);
        Assert.Throws<AiUnavailableException>(() => FoundryAgentFormat.ToText(AiPrompts.PatientMessage, "Dear patient, SYN free text"));
    }

    [Fact]
    public void A_use_or_a_situation_with_no_translation_fails_closed()
    {
        Assert.Throws<AiUnavailableException>(() => FoundryAgentFormat.ToAgentInput(new AiRequest("another-use", "SYN task", "{}", "SYN prompt"), synthetic: true));
        Assert.Throws<AiUnavailableException>(() => FoundryAgentFormat.ToAgentInput(
            new AiRequest(AiPrompts.PatientMessage, "SYN task", """{"situation":"share the result","clinic":"SYN","channel":"email","max_characters":1000}""", "SYN prompt"),
            synthetic: true));
    }

    private static IServiceCollection Services(string? provider, string? endpoint)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ai:Provider"] = provider, ["Ai:Foundry:ProjectEndpoint"] = endpoint, ["Ai:Foundry:Agents:patient-message"] = "syn-patient-agent",
        }).Build();
        return new ServiceCollection().AddAi(configuration);
    }

    private static FoundryAiClient Client(StubHandler handler, TokenCredential credential) =>
        new(new HttpClient(handler), credential, Options.Create(new AiOptions
        {
            Provider = "Foundry", Foundry = new FoundryOptions { ProjectEndpoint = Endpoint },
        }));

    private static string CopyOfShippedPrompts()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"careloop-prompts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        foreach (var file in Directory.EnumerateFiles(TestPrompts.Directory, "*.md"))
        {
            File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
        }

        return directory;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed record SentRequest(HttpMethod Method, string Uri, string? Authorization, string Body);

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<SentRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new SentRequest(request.Method, request.RequestUri!.ToString(), request.Headers.Authorization?.ToString(),
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            return respond(request);
        }
    }

    private sealed class CountingCredential : TokenCredential
    {
        public int Calls { get; private set; }

        public string[] Scopes { get; private set; } = [];

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            GetTokenAsync(requestContext, cancellationToken).AsTask().GetAwaiter().GetResult();

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            Calls++;
            Scopes = requestContext.Scopes;
            return ValueTask.FromResult(new AccessToken("SYN-TOKEN", DateTimeOffset.UtcNow.AddHours(1)));
        }
    }
}

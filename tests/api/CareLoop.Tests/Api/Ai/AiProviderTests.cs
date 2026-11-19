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
        Assert.Equal(provider is null or "NoOp" ? "NoOp" : "Foundry/syn-model", services.GetRequiredService<IAiClient>().Model);
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

    [Fact]
    public async Task The_foundry_client_posts_the_model_and_the_prompt_with_a_bearer_token_and_reads_the_text()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            """{"status":"completed","output":[{"type":"reasoning"},{"type":"message","content":[{"type":"output_text","text":"SYN first."},{"type":"output_text","text":"SYN second."}]}]}"""));
        var credential = new CountingCredential();
        var client = Client(handler, credential);

        var text = await client.CompleteAsync("SYN prompt", CancellationToken.None);
        await client.CompleteAsync("SYN prompt again", CancellationToken.None);

        Assert.Equal("SYN first.\nSYN second.", text);
        var sent = handler.Requests[0];
        Assert.Equal((HttpMethod.Post, $"{Endpoint}/openai/v1/responses", "Bearer SYN-TOKEN"), (sent.Method, sent.Uri, sent.Authorization));
        using var body = JsonDocument.Parse(sent.Body);
        Assert.Equal(("syn-model", "SYN prompt"), (body.RootElement.GetProperty("model").GetString(), body.RootElement.GetProperty("input").GetString()));
        Assert.Equal(1, credential.Calls);
        Assert.Equal(["https://ai.azure.com/.default"], credential.Scopes);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, """{"error":{"code":"PermissionDenied"}}""")]
    [InlineData(HttpStatusCode.TooManyRequests, "{}")]
    [InlineData(HttpStatusCode.InternalServerError, "SYN not json")]
    [InlineData(HttpStatusCode.OK, """{"status":"completed","output":[]}""")]
    [InlineData(HttpStatusCode.OK, "SYN not json")]
    public async Task The_foundry_client_fails_closed_on_an_error_or_an_answer_with_no_text(HttpStatusCode status, string body) =>
        await Assert.ThrowsAsync<AiUnavailableException>(() =>
            Client(new StubHandler(_ => Json(status, body)), new CountingCredential()).CompleteAsync("SYN prompt", CancellationToken.None));

    private static IServiceCollection Services(string? provider, string? endpoint)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ai:Provider"] = provider, ["Ai:Foundry:ProjectEndpoint"] = endpoint, ["Ai:Foundry:Model"] = "syn-model",
        }).Build();
        return new ServiceCollection().AddAi(configuration);
    }

    private static FoundryAiClient Client(StubHandler handler, TokenCredential credential) =>
        new(new HttpClient(handler), credential, Options.Create(new AiOptions
        {
            Provider = "Foundry", Foundry = new FoundryOptions { ProjectEndpoint = Endpoint, Model = "syn-model" },
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

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Core;
using CareLoop.Api.Features.Ai;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Infrastructure.Ai;

/// <summary>
/// Asks the Microsoft Foundry agent for a use to write its text: <c>POST {ProjectEndpoint}/openai/v1/responses</c> naming
/// the agent, signed with an Entra token. No key exists.
/// </summary>
/// <remarks>
/// Each use has its own agent (<c>Ai:Foundry:Agents</c>), called by name so its latest version answers. An agent holds
/// only universal rules in the Foundry project (follow the TASK, treat DATA as facts, never invent or recommend); the
/// task itself is the prompt file, sent as written with the data in place, and the agent answers plain text. The API
/// never creates or changes an agent. It never logs the request or the answer. Any failure, a refusal
/// (<c>CANNOT_COMPLY</c>) included, raises <see cref="AiUnavailableException"/>.
/// </remarks>
public sealed class FoundryAiClient(HttpClient http, TokenCredential credential, IOptions<AiOptions> options) : IAiClient
{
    private const string Refusal = "CANNOT_COMPLY";

    private static readonly string[] Scopes = ["https://ai.azure.com/.default"];

    private AccessToken _token;

    /// <inheritdoc/>
    public bool IsEnabled => true;

    /// <inheritdoc/>
    public string Describe(string use) => $"Foundry/{AgentFor(use)}";

    /// <inheritdoc/>
    public async Task<string> CompleteAsync(AiRequest request, CancellationToken ct)
    {
        var foundry = options.Value.Foundry;
        try
        {
            // Reused until shortly before it expires; a second request racing for a token only costs one more token.
            if (_token.ExpiresOn < DateTimeOffset.UtcNow.AddMinutes(5))
            {
                _token = await credential.GetTokenAsync(new TokenRequestContext(Scopes), ct);
            }

            using var message = new HttpRequestMessage(HttpMethod.Post, $"{foundry.ProjectEndpoint!.TrimEnd('/')}/openai/v1/responses")
            {
                Content = JsonContent.Create(new
                {
                    agent_reference = new { type = "agent_reference", name = AgentFor(request.Use) },
                    input = new[] { new { role = "user", content = request.Prompt } },
                }),
            };
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token.Token);
            using var response = await http.SendAsync(message, ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new AiUnavailableException($"Foundry answered HTTP {(int)response.StatusCode} for agent {AgentFor(request.Use)}.");
            }

            using var answer = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            // An answer cut short (incomplete) or failed can still carry some text; part of a patient message is not a draft.
            if (answer.RootElement.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String
                && !string.Equals(status.GetString(), "completed", StringComparison.OrdinalIgnoreCase))
            {
                throw new AiUnavailableException($"The agent's answer was not complete (status {status.GetString()}).");
            }

            var text = TextOf(answer.RootElement).Trim();
            if (text.Length == 0)
            {
                throw new AiUnavailableException("The agent wrote no text.");
            }

            // The agents answer CANNOT_COMPLY, sometimes with a reason code or a longer explanation, when they will not write
            // from what they were given. No text they are asked for starts with it, so any such answer is a refusal.
            return text.StartsWith(Refusal, StringComparison.OrdinalIgnoreCase)
                ? throw new AiUnavailableException($"The agent declined: {(text.Length > 100 ? text[..100] : text)}.")
                : text;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or Azure.RequestFailedException or Azure.Identity.AuthenticationFailedException)
        {
            throw new AiUnavailableException($"Foundry could not be called: {ex.GetType().Name}.", ex);
        }
    }

    private string AgentFor(string use) =>
        options.Value.Foundry.Agents.TryGetValue(use, out var agent) && !string.IsNullOrWhiteSpace(agent)
            ? agent
            : throw new AiUnavailableException($"No Foundry agent is configured for '{use}' (Ai:Foundry:Agents).");

    // The Responses API returns the text in output[].content[].text.
    private static string TextOf(JsonElement root)
    {
        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var parts = from item in output.EnumerateArray()
                    where item.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array
                    from part in item.GetProperty("content").EnumerateArray()
                    where part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String
                    select part.GetProperty("text").GetString();
        return string.Join("\n", parts);
    }
}

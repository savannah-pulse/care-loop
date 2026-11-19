using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Core;
using CareLoop.Api.Features.Ai;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Infrastructure.Ai;

/// <summary>
/// Calls a model deployment in a Microsoft Foundry project: <c>POST {ProjectEndpoint}/openai/v1/responses</c> with the
/// prompt, signed with an Entra token. No key exists.
/// </summary>
/// <remarks>It never logs the prompt or the answer. Any failure raises <see cref="AiUnavailableException"/>.</remarks>
public sealed class FoundryAiClient(HttpClient http, TokenCredential credential, IOptions<AiOptions> options) : IAiClient
{
    private static readonly string[] Scopes = ["https://ai.azure.com/.default"];

    private AccessToken _token;

    /// <inheritdoc/>
    public string Model => $"Foundry/{options.Value.Foundry.Model}";

    /// <inheritdoc/>
    public bool IsEnabled => true;

    /// <inheritdoc/>
    public async Task<string> CompleteAsync(string prompt, CancellationToken ct)
    {
        var foundry = options.Value.Foundry;
        try
        {
            // Reused until shortly before it expires; a second request racing for a token only costs one more token.
            if (_token.ExpiresOn < DateTimeOffset.UtcNow.AddMinutes(5))
            {
                _token = await credential.GetTokenAsync(new TokenRequestContext(Scopes), ct);
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{foundry.ProjectEndpoint!.TrimEnd('/')}/openai/v1/responses")
            {
                Content = JsonContent.Create(new { model = foundry.Model, input = prompt }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token.Token);
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new AiUnavailableException($"Foundry answered HTTP {(int)response.StatusCode}.");
            }

            using var answer = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var text = TextOf(answer.RootElement);
            return string.IsNullOrWhiteSpace(text) ? throw new AiUnavailableException("Foundry wrote no text.") : text;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or Azure.RequestFailedException or Azure.Identity.AuthenticationFailedException)
        {
            throw new AiUnavailableException($"Foundry could not be called: {ex.GetType().Name}.", ex);
        }
    }

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

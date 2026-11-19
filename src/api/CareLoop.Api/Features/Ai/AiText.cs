using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CareLoop.Api.Infrastructure.Ai;
using CareLoop.Contracts.Ai;
using CareLoop.Domain.Time;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Features.Ai;

/// <summary>
/// What the three AI endpoints share: put the data into the prompt file, ask the model, and answer 200, 409 or 503.
/// </summary>
/// <remarks>
/// An answer is kept in memory for <c>Ai:CacheHours</c> under the provider and the exact prompt, so the same data is not
/// sent twice; any change in the data is a different prompt. <c>refresh</c> asks again and replaces the kept answer.
/// </remarks>
public sealed class AiText(IAiClient ai, AiPrompts prompts, IMemoryCache cache, IOptions<AiOptions> options, IClock clock, ILogger<AiText> logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        // Only what there is to say is sent: a field with nothing in it is left out.
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Whether AI is switched on.</summary>
    public bool IsOn => ai.IsEnabled;

    /// <summary>The 409 answer while <c>Ai:Provider</c> is <c>NoOp</c>.</summary>
    /// <returns>A problem saying AI is switched off; nothing was read or sent.</returns>
    public static IResult SwitchedOff() =>
        Results.Problem(title: "AI is switched off", detail: "Ai:Provider is NoOp. Nothing was sent.", statusCode: StatusCodes.Status409Conflict);

    /// <summary>Builds the request for a use: the prompt file with the data, as JSON, in place.</summary>
    /// <param name="promptName">The prompt file's name, for example <see cref="AiPrompts.RoleBriefing"/>.</param>
    /// <param name="data">The data to summarise, already limited to what may be sent.</param>
    /// <returns>The request exactly as a provider receives it.</returns>
    public AiRequest Request(string promptName, object data) =>
        new(promptName, prompts.Render(promptName, JsonSerializer.Serialize(data, data.GetType(), Json)));

    /// <summary>Asks for a text written from the data, reusing a kept answer when the request is unchanged.</summary>
    /// <param name="promptName">The prompt file's name.</param>
    /// <param name="data">The data to summarise, already limited to what may be sent.</param>
    /// <param name="refresh"><see langword="true"/> to ask again even when an answer is kept.</param>
    /// <param name="reuse"><see langword="false"/> for a text that is never kept.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <param name="finish">Applied to the text on every request, after any reuse: for words only this reader may be shown, which were never sent.</param>
    /// <returns>200 with <see cref="AiTextResponse"/>, or a 503 problem when the provider could not write it.</returns>
    public async Task<IResult> WriteAsync(string promptName, object data, bool refresh, bool reuse, CancellationToken ct, Func<string, string>? finish = null)
    {
        var request = Request(promptName, data);
        var writer = ai.Describe(promptName);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{writer}|{request.Prompt}")));
        var hours = options.Value.CacheHours;
        reuse = reuse && hours > 0;
        IResult Answer(AiTextResponse response) => TypedResults.Ok(finish is null ? response : response with { Text = finish(response.Text) });
        if (reuse && !refresh && cache.TryGetValue<AiTextResponse>(key, out var kept) && kept is not null)
        {
            return Answer(kept with { Cached = true });
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
            var text = await ai.CompleteAsync(request, timeout.Token);
            var response = new AiTextResponse { Text = text.Trim(), Model = writer, GeneratedAt = clock.UtcNow };
            if (reuse)
            {
                cache.Set(key, response, TimeSpan.FromHours(hours));
            }

            return Answer(response);
        }
        catch (Exception ex) when (ex is AiUnavailableException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            // The reason is logged, never the prompt or the data.
            logger.LogWarning("AI could not write {Prompt} with {Writer}: {Reason}", promptName, writer,
                ex is AiUnavailableException ? ex.Message : $"no answer within {options.Value.TimeoutSeconds} s");
            return Results.Problem(title: "AI could not write this", detail: "Nothing else depends on it. Try again later.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}

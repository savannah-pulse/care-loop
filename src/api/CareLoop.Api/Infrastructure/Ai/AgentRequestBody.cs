using System.Net.Http.Headers;
using System.Text.Json;
using CareLoop.Api.Features.Agents;

namespace CareLoop.Api.Infrastructure.Ai;

/// <summary>
/// Serialises an agent request exactly as it will be sent, and refuses it before any call when it is over
/// <c>Ai:MaxRequestBytes</c>.
/// </summary>
/// <remarks>
/// The same guardrail the handoff summary has: a request that grew beyond what was designed for is worth a person's
/// attention, not a truncated prompt, so it fails closed like any other failure and nothing leaves CareLoop.
/// </remarks>
internal static class AgentRequestBody
{
    /// <summary>
    /// Serialises the body and checks its size.
    /// </summary>
    /// <typeparam name="T">The body's type.</typeparam>
    /// <param name="body">The body, with its wire names on its properties.</param>
    /// <param name="request">The request it carries, named in a refusal.</param>
    /// <param name="maxBytes">The limit, <c>Ai:MaxRequestBytes</c>.</param>
    /// <returns>The JSON content to send.</returns>
    /// <exception cref="AgentUnavailableException">The body is over the limit; nothing is sent.</exception>
    public static ByteArrayContent Create<T>(T body, AgentRequest request, int maxBytes)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(body);
        if (bytes.Length > maxBytes)
        {
            throw new AgentUnavailableException(request.Agent.Name,
                $"The request was larger than the configured limit ({bytes.Length} of {maxBytes} bytes), so nothing was sent.");
        }

        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        return content;
    }
}

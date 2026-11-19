namespace CareLoop.Api.Features.Ai;

/// <summary>
/// Sends one prompt to an AI model and returns the text it wrote. The only thing the AI endpoints know about a provider.
/// </summary>
/// <remarks>
/// <c>Ai:Provider</c> chooses the implementation: <c>NoOp</c> (AI off) or <c>Foundry</c>. To use another provider, write
/// one more class that implements this and register it in <c>DependencyInjection.AddAi</c>; the endpoints and the prompt
/// files do not change. The prompt already holds only what the endpoint may send.
/// </remarks>
public interface IAiClient
{
    /// <summary>Names what writes a use's text, for example <c>Foundry/role-briefing</c>; shown with the text.</summary>
    /// <param name="use">The use, for example <c>role-briefing</c>.</param>
    /// <returns>The provider and its model or agent.</returns>
    string Describe(string use);

    /// <summary><see langword="false"/> when AI is switched off and nothing may be sent.</summary>
    bool IsEnabled { get; }

    /// <summary>Asks for one text and returns it.</summary>
    /// <param name="request">The use and its prompt.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The text, never empty.</returns>
    /// <exception cref="AiUnavailableException">The provider could not be reached, refused, answered an error or wrote nothing.</exception>
    Task<string> CompleteAsync(AiRequest request, CancellationToken ct);
}

/// <summary>One request for a text.</summary>
/// <param name="Use">Which text: the prompt file's name, for example <c>role-briefing</c>. A provider with one agent or model per use chooses by it.</param>
/// <param name="Prompt">The prompt file with the data in place: the task and the facts, everything the provider is sent.</param>
public sealed record AiRequest(string Use, string Prompt);

/// <summary>The AI provider could not write the text. Never replaced by stand-in words.</summary>
/// <param name="message">Why, in words safe to log; never the prompt.</param>
/// <param name="innerException">The underlying failure, if any.</param>
public sealed class AiUnavailableException(string message, Exception? innerException = null) : Exception(message, innerException);

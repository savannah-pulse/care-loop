namespace CareLoop.Api.Features.Ai;

/// <summary>
/// Sends one prompt to an AI model and returns the text it wrote. The only thing the AI endpoints know about a provider.
/// </summary>
/// <remarks>
/// <c>Ai:Provider</c> chooses the implementation: <c>NoOp</c> (AI off) or <c>Foundry</c>. To use another provider, write
/// one more class that implements this and register it in <c>DependencyInjection.AddAi</c>; the endpoints and the prompt
/// files do not change. A provider that takes a prompt sends <see cref="AiRequest.Prompt"/>; one with its own input
/// format (the Foundry agents) builds it from the parts. Every part already holds only what the endpoint may send.
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
    /// <param name="request">The use, its instructions and its data.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The text, never empty.</returns>
    /// <exception cref="AiUnavailableException">The provider could not be reached, refused, answered an error or wrote nothing.</exception>
    Task<string> CompleteAsync(AiRequest request, CancellationToken ct);
}

/// <summary>One request for a text.</summary>
/// <param name="Use">Which text: the prompt file's name, for example <c>role-briefing</c>.</param>
/// <param name="Task">The prompt file's instructions, without the data.</param>
/// <param name="Data">The data to summarise, as JSON.</param>
/// <param name="Prompt">The prompt file with the data in place: everything a provider that takes one prompt needs.</param>
public sealed record AiRequest(string Use, string Task, string Data, string Prompt);

/// <summary>The AI provider could not write the text. Never replaced by stand-in words.</summary>
/// <param name="message">Why, in words safe to log; never the prompt.</param>
/// <param name="innerException">The underlying failure, if any.</param>
public sealed class AiUnavailableException(string message, Exception? innerException = null) : Exception(message, innerException);

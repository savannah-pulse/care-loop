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
    /// <summary>The provider and model, for example <c>Foundry/gpt-6.1-sol</c>; shown with the text.</summary>
    string Model { get; }

    /// <summary><see langword="false"/> when AI is switched off and nothing may be sent.</summary>
    bool IsEnabled { get; }

    /// <summary>Sends the prompt and returns the model's text.</summary>
    /// <param name="prompt">The whole prompt: the instructions with the data in place.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The text, never empty.</returns>
    /// <exception cref="AiUnavailableException">The model could not be reached, answered an error or wrote nothing.</exception>
    Task<string> CompleteAsync(string prompt, CancellationToken ct);
}

/// <summary>The AI provider could not write the text. Never replaced by stand-in words.</summary>
/// <param name="message">Why, in words safe to log; never the prompt.</param>
/// <param name="innerException">The underlying failure, if any.</param>
public sealed class AiUnavailableException(string message, Exception? innerException = null) : Exception(message, innerException);

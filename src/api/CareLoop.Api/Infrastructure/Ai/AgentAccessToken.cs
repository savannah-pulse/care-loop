using Azure;
using Azure.Core;
using Azure.Identity;
using CareLoop.Api.Features.Agents;
using CareLoop.Domain.Time;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Infrastructure.Ai;

/// <summary>
/// Supplies the Entra access token the Foundry agents are called with.
/// </summary>
/// <remarks>
/// <para>
/// Normally the token comes from the API's own identity through a <see cref="TokenCredential"/>:
/// <c>DefaultAzureCredential</c>, which is the managed identity when hosted in Azure (a user-assigned one is chosen with
/// <c>AZURE_CLIENT_ID</c>) and the developer's sign-in locally. No API key exists to leak. A token is reused until five
/// minutes before it expires, so a page that asks three agents does not ask for three tokens.
/// </para>
/// <para>
/// In the <c>Development</c> environment only, a token set in <see cref="AgentOptions.AccessToken"/> is used as it is,
/// so a developer can test with a token from their own Windows sign-in. Anywhere else that setting is ignored and a
/// warning is logged once, so a token copied into a deployed configuration never authenticates anything.
/// </para>
/// </remarks>
public sealed class AgentAccessToken
{
    /// <summary>The scope tokens are requested for: the Foundry data plane.</summary>
    public const string Scope = "https://ai.azure.com/.default";

    private static readonly TimeSpan RefreshBefore = TimeSpan.FromMinutes(5);

    private readonly TokenCredential _credential;
    private readonly IClock _clock;
    private readonly string? _developmentToken;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AccessToken? _cached;

    /// <summary>
    /// Creates the token source.
    /// </summary>
    /// <param name="credential">The API's identity, used whenever no development token applies.</param>
    /// <param name="options">Supplies the development token, if any.</param>
    /// <param name="environment">Decides whether the development token may be used.</param>
    /// <param name="clock">Tells when a cached token is close to expiring.</param>
    /// <param name="logger">Warns when a development token is set outside Development.</param>
    public AgentAccessToken(TokenCredential credential, IOptions<AgentOptions> options, IHostEnvironment environment,
        IClock clock, ILogger<AgentAccessToken> logger)
    {
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        _credential = credential;
        _clock = clock;
        var token = options.Value.AccessToken?.Trim();
        if (string.IsNullOrEmpty(token))
        {
            return;
        }

        if (environment.IsDevelopment())
        {
            _developmentToken = token;
        }
        else
        {
            logger.LogWarning("Ai:Agents:AccessToken is ignored in the {Environment} environment; the API calls the agents with its own identity.",
                environment.EnvironmentName);
        }
    }

    /// <summary>
    /// Returns a token for <see cref="Scope"/>.
    /// </summary>
    /// <param name="agentName">The agent the token is for, named in the failure.</param>
    /// <param name="ct">Cancels the request, including the configured timeout.</param>
    /// <returns>The bearer token.</returns>
    /// <exception cref="AgentUnavailableException">No token could be obtained for the API's identity.</exception>
    public async Task<string> GetAsync(string agentName, CancellationToken ct)
    {
        if (_developmentToken is not null)
        {
            return _developmentToken;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_cached is { } cached && cached.ExpiresOn - RefreshBefore > _clock.UtcNow)
            {
                return cached.Token;
            }

            var fresh = await _credential.GetTokenAsync(new TokenRequestContext([Scope]), ct);
            _cached = fresh;
            return fresh.Token;
        }
        catch (Exception ex) when (ex is AuthenticationFailedException or RequestFailedException)
        {
            throw new AgentUnavailableException(agentName, "No Entra token could be obtained for the API's identity.", innerException: ex);
        }
        finally
        {
            _gate.Release();
        }
    }
}

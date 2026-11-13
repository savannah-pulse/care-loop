using CareLoop.Api.Features.Notifications;
using CareLoop.Domain.Recipients;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Infrastructure.Notifications;

/// <summary>
/// Notification provider that delivers to the simulators instead of a real channel.
/// </summary>
/// <remarks>
/// <para>
/// When this provider is enabled, notifications go to the simulators: a patient message to the
/// patient-channel simulator, as a FHIR <c>CommunicationRequest</c> carrying the rendered
/// message and the notification attempt identifier, and a staff message to the HIS simulator's
/// staff inbox, choosing by <see cref="Recipient.Kind"/>. It is the only provider enabled until
/// SMS and email are built.
/// </para>
/// <para>
/// What the simulator sends back (delivery reports and the patient's replies) arrives through
/// this provider's inbound half, <see cref="SimulatorInboundNotificationHandler"/>.
/// </para>
/// </remarks>
public sealed class SimulatorNotificationProvider : INotificationProvider
{
    private readonly HttpClient _http;
    private readonly IOptions<SimulatorNotificationOptions> _endpoints;

    /// <summary>
    /// Creates the provider.
    /// </summary>
    /// <param name="http">The HTTP client supplied by the HTTP client factory.</param>
    /// <param name="endpoints">Where the simulators can be reached.</param>
    public SimulatorNotificationProvider(HttpClient http, IOptions<SimulatorNotificationOptions> endpoints)
    {
        _http = http;
        _endpoints = endpoints;
    }

    /// <inheritdoc/>
    public NotificationChannel Channel => NotificationChannel.Simulator;

    /// <inheritdoc/>
    public bool AllowsSensitiveContent => true;

    /// <inheritdoc/>
    public bool CanReach(Recipient recipient)
    {
        // TODO(WS-4): true when the recipient has a Simulator contact point.
        throw new NotImplementedException("WS4: SimulatorNotificationProvider.CanReach");
    }

    /// <inheritdoc/>
    public Task<SendResult> SendAsync(NotificationMessage message, CancellationToken ct)
    {
        // TODO(WS-4): send a patient message as a CommunicationRequest to the patient-channel simulator and a
        // staff message to the HIS simulator's staff inbox, and map the answer to SendResult.
        throw new NotImplementedException("WS4: SimulatorNotificationProvider.SendAsync");
    }
}

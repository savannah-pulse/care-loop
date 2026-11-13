using CareLoop.Api.Features.Notifications;
using CareLoop.Contracts.Events;
using CareLoop.Domain.Recipients;

namespace CareLoop.Api.Infrastructure.Notifications;

/// <summary>
/// Understands what the patient-channel simulator sends back to CareLoop.
/// </summary>
/// <remarks>
/// <para>
/// The simulator plays the patient: it receives a notification and then responds as the scenario
/// under test requires (delivers or fails, acknowledges, confirms understanding, asks for a
/// call, or stays silent). It reports each of those as a FHIR <c>Communication</c>, which is this
/// provider's private wire format.
/// </para>
/// <para>
/// Will translate status <c>completed</c> to delivered and <c>not-done</c> (with its reason) to
/// failed for delivery reports, and use the category to distinguish the patient's
/// acknowledgement, teach-back and call-me request, producing a notification update event for
/// each. A delivery report is never translated to an acknowledgement.
/// </para>
/// </remarks>
public sealed class SimulatorInboundNotificationHandler : IInboundNotificationHandler
{
    /// <inheritdoc/>
    public NotificationChannel Channel => NotificationChannel.Simulator;

    /// <inheritdoc/>
    public Task<IReadOnlyList<EventEnvelope>> TranslateAsync(InboundNotificationMessage message, CancellationToken ct)
    {
        // TODO(WS-4): parse the body as a FHIR Communication and map status and category to a
        // NotificationUpdatePayload, using the Communication id and version as the source event id.
        throw new NotImplementedException("WS4: SimulatorInboundNotificationHandler.TranslateAsync");
    }
}

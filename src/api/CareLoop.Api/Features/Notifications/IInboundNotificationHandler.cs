using CareLoop.Contracts.Events;
using CareLoop.Domain.Recipients;

namespace CareLoop.Api.Features.Notifications;

/// <summary>
/// The inbound half of a notification provider: understands what comes back on its channel.
/// </summary>
/// <remarks>
/// <para>
/// A channel is two-way. <see cref="INotificationProvider"/> sends a message out; this interface
/// receives what the channel sends back: delivery reports, failures, and what the patient does
/// in reply (acknowledging, confirming they understood, asking for a call). Each provider speaks
/// its own format, so each has its own handler, selected by <see cref="Channel"/>.
/// </para>
/// <para>
/// A handler only verifies and translates. It turns a provider message into internal
/// <see cref="EventEnvelope"/>s and holds no business logic; the envelopes then go through
/// <c>EventIngestor</c> like every other event, so they are validated, deduplicated, applied
/// and audited in the one intake pipeline.
/// </para>
/// </remarks>
public interface IInboundNotificationHandler
{
    /// <summary>The channel whose inbound messages this handler understands. Matches the provider it belongs to.</summary>
    NotificationChannel Channel { get; }

    /// <summary>
    /// Verifies a provider message and translates it into internal events.
    /// </summary>
    /// <param name="message">The message as it arrived.</param>
    /// <param name="ct">Cancels the translation.</param>
    /// <returns>
    /// The events the message represents: usually one, none when the message carries nothing
    /// CareLoop tracks. Each envelope's source event identifier must be stable for the same
    /// provider message, so a redelivery is deduplicated.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">The message could not be verified as coming from the provider.</exception>
    /// <exception cref="FormatException">The message is not in the provider's format.</exception>
    Task<IReadOnlyList<EventEnvelope>> TranslateAsync(InboundNotificationMessage message, CancellationToken ct);
}

using Hl7.Fhir.Model;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Inbound;

/// <summary>
/// Translates an inbound FHIR <c>CommunicationRequest</c> into a simulated inbox message.
/// </summary>
/// <remarks>
/// Pure mapping with no side effects. The message body is taken verbatim from the request
/// payload: CareLoop chooses the template per channel (a channel that does not allow sensitive
/// content gets the SMS-safe text), and the simulator must show exactly what it was sent so a
/// reviewer can verify that no sensitive detail reached an SMS-like channel.
/// </remarks>
public sealed class CommunicationRequestMapper
{
    /// <summary>
    /// Maps a request to a new inbox message in the <see cref="DeliveryState.Pending"/> and
    /// <see cref="ResponseState.None"/> states.
    /// </summary>
    /// <param name="request">The parsed FHIR R4 <c>CommunicationRequest</c>.</param>
    /// <param name="receivedAt">When the simulator received the request.</param>
    /// <returns>The inbox message, marked as simulated.</returns>
    /// <exception cref="FormatException">
    /// The request has no id, no patient subject, no text payload or an unrecognised medium, or
    /// its patient identifier is not synthetic (not prefixed <c>SYN-</c>).
    /// </exception>
    public InboxMessage Map(CommunicationRequest request, DateTimeOffset receivedAt)
    {
        // TODO(WS-8): read id, subject (synthetic id + display), medium -> InboxChannel, payload text,
        // language and the case Task reference (basedOn/about); reject non-synthetic identifiers.
        throw new NotImplementedException("WS8: CommunicationRequestMapper.Map");
    }
}

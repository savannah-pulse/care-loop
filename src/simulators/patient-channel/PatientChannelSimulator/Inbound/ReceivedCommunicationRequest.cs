using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Inbound;

/// <summary>
/// The outcome of receiving one <c>CommunicationRequest</c>.
/// </summary>
/// <param name="Message">The inbox message for the request, as it is now.</param>
/// <param name="Created">Whether this call created the message; <see langword="false"/> when the request was a resend.</param>
public sealed record ReceivedCommunicationRequest(InboxMessage Message, bool Created);

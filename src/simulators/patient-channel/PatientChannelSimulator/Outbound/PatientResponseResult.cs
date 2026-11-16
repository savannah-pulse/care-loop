using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Outbound;

/// <summary>
/// The outcome of one patient response.
/// </summary>
/// <param name="Status">How the attempt ended.</param>
/// <param name="Message">The inbox message as it is now; <see langword="null"/> only when <paramref name="Status"/> is <see cref="PatientResponseStatus.NotFound"/>.</param>
/// <param name="Send">CareLoop's answer; <see langword="null"/> when nothing was sent or CareLoop could not be reached.</param>
/// <param name="Error">Why the response was not accepted; <see langword="null"/> on success.</param>
public sealed record PatientResponseResult(PatientResponseStatus Status, InboxMessage? Message, FhirSendResult? Send, string? Error = null);

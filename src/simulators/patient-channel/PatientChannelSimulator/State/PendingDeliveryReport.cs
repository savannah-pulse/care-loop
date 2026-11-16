namespace PatientChannelSimulator.State;

/// <summary>
/// A delivery outcome that could not be reported to CareLoop yet and is waiting to be tried again.
/// </summary>
/// <param name="MessageId">The inbox message whose delivery outcome is still unreported.</param>
/// <param name="Attempt">Which background retry this is, counting from 1.</param>
/// <param name="DueAt">When the retry is due, on the simulator clock.</param>
public sealed record PendingDeliveryReport(string MessageId, int Attempt, DateTimeOffset DueAt);

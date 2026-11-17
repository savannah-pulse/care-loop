namespace PatientChannelSimulator.State;

/// <summary>
/// A reply a simulated patient will send once its due time has passed.
/// </summary>
/// <remarks>
/// Created when a delivered notification is one the patient's behaviour replies to after a delay.
/// The response dispatcher sends it; replacing the patient's behaviour drops it.
/// </remarks>
/// <param name="MessageId">The inbox message being replied to.</param>
/// <param name="Text">The reply the patient will type.</param>
/// <param name="DueAt">When the reply is due, on the simulator clock.</param>
public sealed record PendingResponse(string MessageId, string Text, DateTimeOffset DueAt);

namespace PatientChannelSimulator.State;

/// <summary>
/// A record of one reaction to one notification: what the simulator did and how CareLoop answered.
/// </summary>
/// <remarks>
/// Reactions are the evidence a test reads back. They exist only for notifications that were
/// actually received, which is how a missing notification shows up as a missing reaction.
/// </remarks>
/// <param name="MessageId">The inbox message the reaction belongs to.</param>
/// <param name="PatientSyntheticId">Synthetic identifier of the patient who reacted.</param>
/// <param name="Reaction">What was done.</param>
/// <param name="Automatic">Whether the reaction came from the patient's behaviour (<see langword="true"/>) or from someone acting in the ClientApp.</param>
/// <param name="SourceEventId">The deterministic identifier given to the emitted <c>Communication</c>; <see langword="null"/> when nothing was sent.</param>
/// <param name="At">When the reaction happened, by the simulator clock.</param>
/// <param name="HttpStatusCode">CareLoop's answer; <see langword="null"/> when nothing was sent or CareLoop could not be reached.</param>
/// <param name="Succeeded">Whether the reaction was carried out; for one that sends, whether CareLoop accepted it.</param>
/// <param name="Error">Why the reaction failed; <see langword="null"/> on success.</param>
public sealed record PatientReactionRecord(
    string MessageId,
    string PatientSyntheticId,
    PatientReactionKind Reaction,
    bool Automatic,
    string? SourceEventId,
    DateTimeOffset At,
    int? HttpStatusCode,
    bool Succeeded,
    string? Error);

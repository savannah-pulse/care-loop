using PatientChannelSimulator.Outbound;

namespace PatientChannelSimulator.State;

/// <summary>
/// A patient response that the patient's behaviour has decided on and that is waiting for its due time.
/// </summary>
/// <remarks>
/// Created when a behaviour's <see cref="PatientBehaviour.RespondAfter"/> is longer than zero. The
/// response dispatcher sends it once the simulator clock reaches <paramref name="DueAt"/>.
/// </remarks>
/// <param name="MessageId">The inbox message the patient will respond to.</param>
/// <param name="Kind">Which response the patient will give.</param>
/// <param name="Confidence">The 1 to 5 rating to send: the value of a <see cref="PatientResponseKind.ConfidenceRating"/>, or the rating that follows another response; <see langword="null"/> when the patient gives none.</param>
/// <param name="DueAt">When the response is due, on the simulator clock.</param>
public sealed record PendingResponse(string MessageId, PatientResponseKind Kind, int? Confidence, DateTimeOffset DueAt);

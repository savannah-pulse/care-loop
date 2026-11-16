namespace PatientChannelSimulator.Inbound;

/// <summary>
/// Body of <c>POST /api/inbox/{messageId}/rate-confidence</c> (C10).
/// </summary>
/// <param name="Rating">The patient's confidence, a whole number from 1 to 5.</param>
public sealed record ConfidenceRatingRequest(int Rating);

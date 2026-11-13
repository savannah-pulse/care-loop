namespace HisSimulator.Outbound;

/// <summary>
/// Body of <c>POST /actions/outcome</c>: a clinician records a follow-up outcome.
/// </summary>
/// <param name="CaseRef">Simulator-side handle of the case the outcome belongs to.</param>
/// <param name="Performer">The synthetic clinician recording the outcome.</param>
/// <param name="EvidenceKind">Placeholder evidence-kind code from the pathway definition; never a clinical finding.</param>
/// <param name="AsFollowUpReport">
/// <see langword="true"/> to send a follow-up <c>DiagnosticReport</c>; <see langword="false"/> to send a <c>Procedure</c>.
/// </param>
public sealed record RecordOutcomeActionRequest(string CaseRef, ActingStaff Performer, string EvidenceKind, bool AsFollowUpReport);

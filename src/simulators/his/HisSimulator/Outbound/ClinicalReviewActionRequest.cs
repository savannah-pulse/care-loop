namespace HisSimulator.Outbound;

/// <summary>
/// Body of <c>POST /actions/clinical-review</c>: a clinician records that they reviewed a result.
/// </summary>
/// <param name="CaseRef">Simulator-side handle of the case being reviewed.</param>
/// <param name="Performer">The synthetic clinician performing the review.</param>
/// <param name="Urgency">
/// The reviewed urgency as a placeholder code from the pathway definition (for example a code offered by the
/// ClientApp from configuration). The simulator passes it through and never chooses one.
/// </param>
public sealed record ClinicalReviewActionRequest(string CaseRef, ActingStaff Performer, string Urgency);

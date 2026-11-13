namespace CareLoop.Api.Features.Summaries;

/// <summary>
/// What a draft is for. A provider's prompt and the facts it may see both depend on it.
/// </summary>
public enum SummaryPurpose
{
    /// <summary>A summary for the clinician receiving a handoff. The only purpose built today.</summary>
    ClinicianHandoff = 0,

    /// <summary>A patient-facing explanation of the stage. Not in scope: patient wording comes from approved templates.</summary>
    PatientStageExplanation = 1,
}

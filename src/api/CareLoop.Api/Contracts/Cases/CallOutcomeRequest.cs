namespace CareLoop.Contracts.Cases;

/// <summary>
/// What a nurse records after speaking to the patient on the phone
/// (<c>POST /cases/{caseId}/call-outcome</c>).
/// </summary>
/// <remarks>
/// Patients are reached by text message and email only, and neither can carry teach-back or a rating,
/// so a person records them after a call. At least one of the three must be recorded. Each is kept
/// apart: saying the next step back is not the same as saying she will come, and a rating is a
/// measure, never an acknowledgement.
/// </remarks>
public sealed record CallOutcomeRequest
{
    /// <summary><see langword="true"/> when the patient said she will come: an acknowledgement.</summary>
    public bool Acknowledged { get; init; }

    /// <summary><see langword="true"/> when the patient said the next step back in her own words (teach-back).</summary>
    public bool Understood { get; init; }

    /// <summary>How confident the patient said she feels about what happens next, from 1 to 5; <see langword="null"/> when she was not asked or gave no rating.</summary>
    public int? Confidence { get; init; }
}

namespace HisSimulator.Outbound;

/// <summary>
/// Body of <c>POST /actions/closure</c>: an authorized member of staff decides to close a case.
/// </summary>
/// <remarks>
/// The simulator forwards the decision as given. Whether it is sufficient (authorized role, evidence
/// or documented exception) is decided by CareLoop's closure gate, and a refusal is reported back.
/// </remarks>
/// <param name="CaseRef">Simulator-side handle of the case to close.</param>
/// <param name="Performer">The synthetic member of staff authorizing the closure; named in the <c>Provenance</c>.</param>
/// <param name="ClosureReason">
/// Typed closure reason: one of <c>Treated</c>, <c>OnSurveillance</c>, <c>NegativeOnConfirmation</c>, <c>Declined</c>,
/// <c>TransferredAndAccepted</c> or <c>Untraceable</c> (any case), or the equivalent wire code such as <c>on-surveillance</c>.
/// The simulator sends the kebab-case wire code; any other value is refused with 400.
/// </param>
/// <param name="EvidenceResourceKey">
/// Key of an outcome resource previously sent for the case (see <see cref="State.SimulatedCase.ResourceIds"/>) to cite as
/// evidence; a key that does not name an outcome is refused with 400. <see langword="null"/> sends no evidence reference.
/// </param>
/// <param name="ExceptionNote">Documented exception text when closing without outcome evidence; otherwise <see langword="null"/>.</param>
/// <param name="NextSurveillanceDate">Optional source-recorded next date, <c>yyyy-MM-dd</c> exactly (a date, not a timestamp).</param>
/// <param name="DocumentedAttemptReferences">Optional source evidence for documented contact attempts.</param>
public sealed record AuthorizeClosureActionRequest(
    string CaseRef,
    ActingStaff Performer,
    string ClosureReason,
    string? EvidenceResourceKey,
    string? ExceptionNote,
    string? NextSurveillanceDate = null,
    IReadOnlyList<string>? DocumentedAttemptReferences = null);

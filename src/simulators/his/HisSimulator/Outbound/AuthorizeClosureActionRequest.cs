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
/// <param name="ClosureReason">Typed closure reason key from the pathway definition, for example <c>Treated</c>.</param>
/// <param name="EvidenceResourceKey">
/// Key of a resource previously sent for the case (see <see cref="State.SimulatedCase.ResourceIds"/>) to cite as evidence;
/// <see langword="null"/> to send no evidence reference.
/// </param>
/// <param name="ExceptionNote">Documented exception text when closing without outcome evidence; otherwise <see langword="null"/>.</param>
public sealed record AuthorizeClosureActionRequest(
    string CaseRef,
    ActingStaff Performer,
    string ClosureReason,
    string? EvidenceResourceKey,
    string? ExceptionNote);

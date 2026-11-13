using System.Text.Json;
using HisSimulator.State;

namespace HisSimulator.Outbound;

/// <summary>
/// One event the simulated HIS is asked to send to CareLoop, whether scripted by a scenario step
/// or triggered manually from the "act as clinician" screen.
/// </summary>
/// <param name="Kind">The kind of event, which decides the FHIR resources that are built.</param>
/// <param name="CaseRef">Simulator-side handle of the case the event belongs to.</param>
/// <param name="SourceEventKey">
/// Deterministic key for this event (for a scenario: run key plus step id). Resource logical ids and
/// versions are derived from it, so resending the same event produces the same source event id and
/// CareLoop deduplicates it.
/// </param>
/// <param name="OccurredAt">The simulated instant stamped on the resources (authored, issued or recorded time).</param>
/// <param name="Status">
/// Track or task status for the event, as used in scenario files: <c>requested</c>, <c>accepted</c>,
/// <c>rejected</c>, <c>completed</c>, <c>booked</c>, <c>fulfilled</c> or <c>noshow</c>. Unused by some kinds.
/// </param>
/// <param name="Performer">The synthetic member of staff performing the event, when one applies.</param>
/// <param name="Patient">The synthetic patient; required when <paramref name="Kind"/> is <see cref="HisEventKind.ScreeningResult"/>.</param>
/// <param name="PathwayId">Identifier of the pathway definition; required when a screening result opens a new case.</param>
/// <param name="Params">
/// Action-specific values passed through from the scenario or the manual action (for example
/// <c>resultCategory</c>, <c>urgency</c>, <c>evidenceKind</c>, <c>closureReason</c>, <c>proposedOwner</c>).
/// Clinical values are placeholder codes from the pathway definition; the simulator never invents them.
/// </param>
public sealed record OutboundEventRequest(
    HisEventKind Kind,
    string CaseRef,
    string SourceEventKey,
    DateTimeOffset OccurredAt,
    string? Status,
    ActingStaff? Performer,
    SimulatedPatient? Patient,
    string? PathwayId,
    IReadOnlyDictionary<string, JsonElement> Params);

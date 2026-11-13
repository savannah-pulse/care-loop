using System.Text.Json;

namespace HisSimulator.Scenarios;

/// <summary>
/// One scripted event in a scenario (<c>$defs/step</c> in the schema).
/// </summary>
public sealed record ScenarioStep
{
    /// <summary>
    /// Stable step identifier. Combined with the run it yields a deterministic source event key,
    /// so replaying a step is deduplicated by CareLoop.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// ISO 8601 duration offset from scenario start, for example <c>PT0S</c> or <c>P2D</c>.
    /// </summary>
    public required string At { get; init; }

    /// <summary>Which simulator performs the step; this service runs only <see cref="ScenarioActor.His"/> steps.</summary>
    public required ScenarioActor Actor { get; init; }

    /// <summary>The kind of event to emit.</summary>
    public required ScenarioAction Action { get; init; }

    /// <summary>Local handle for the case (one actionable result) the step belongs to.</summary>
    public required string CaseRef { get; init; }

    /// <summary>Refers to <see cref="ScenarioPatient.Ref"/>; required on <see cref="ScenarioAction.ScreeningResult"/> steps.</summary>
    public string? PatientRef { get; init; }

    /// <summary>
    /// Track or task status carried by the step, for example <c>requested</c>, <c>accepted</c>, <c>rejected</c>,
    /// <c>booked</c>, <c>fulfilled</c>, <c>noshow</c> or <c>completed</c>.
    /// </summary>
    public string? Status { get; init; }

    /// <summary>The synthetic member of staff who performs the step, when one is named.</summary>
    public ScenarioStaff? Performer { get; init; }

    /// <summary>
    /// Action-specific values, kept as raw JSON because the schema leaves them open.
    /// </summary>
    /// <remarks>
    /// Clinical values here (result category, urgency, evidence kind, closure reason) are placeholder
    /// codes from the pathway definition. The simulator passes them through and never invents its own.
    /// </remarks>
    public IReadOnlyDictionary<string, JsonElement>? Params { get; init; }
}

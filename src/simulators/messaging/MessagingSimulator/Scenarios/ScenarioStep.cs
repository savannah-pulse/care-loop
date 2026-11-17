using System.Text.Json;

namespace MessagingSimulator.Scenarios;

/// <summary>
/// One scripted event in a scenario (<c>$defs/step</c> in the schema).
/// </summary>
/// <remarks>
/// Steps are hospital-system events and belong to the HIS simulator. This simulator reads
/// them only so the shared file parses; it never executes a step.
/// </remarks>
public sealed record ScenarioStep
{
    /// <summary>Stable step identifier; used to build a deterministic source event ID so a replay is deduplicated.</summary>
    public required string Id { get; init; }

    /// <summary>ISO 8601 duration offset from scenario start, for example <c>PT0S</c> or <c>P2D</c>.</summary>
    public required string At { get; init; }

    /// <summary>Which simulator performs the step. Only <c>his</c> performs steps; a patient's reactions follow from their behaviour.</summary>
    public required string Actor { get; init; }

    /// <summary>The kind of event to emit; one value per CareLoop inbound event type.</summary>
    public required string Action { get; init; }

    /// <summary>Local handle for the case (one actionable result) the step belongs to.</summary>
    public required string CaseRef { get; init; }

    /// <summary>Reference to a <see cref="ScenarioPatient.Ref"/>; required on screening-result steps.</summary>
    public string? PatientRef { get; init; }

    /// <summary>Track or task status carried by the step, for example <c>delivered</c> or <c>acknowledged</c>.</summary>
    public string? Status { get; init; }

    /// <summary>The staff member performing the step, when the step has one.</summary>
    public ScenarioStaff? Performer { get; init; }

    /// <summary>Action-specific values. Clinical values are placeholder codes from the pathway definition.</summary>
    public IReadOnlyDictionary<string, JsonElement>? Params { get; init; }
}

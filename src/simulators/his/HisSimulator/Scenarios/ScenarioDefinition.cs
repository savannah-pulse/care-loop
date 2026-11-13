using System.Text.Json.Serialization;

namespace HisSimulator.Scenarios;

/// <summary>
/// A scenario file: a scripted, synthetic sequence of events replayed against CareLoop's FHIR boundary.
/// Mirrors <c>simulators/scenarios/scenario.schema.json</c>.
/// </summary>
/// <remarks>
/// A scenario is either step-based (<see cref="Patients"/> and <see cref="Steps"/>) or a
/// <see cref="Cohort"/> that mixes step-based scenarios; the schema allows exactly one of the two forms.
/// </remarks>
public sealed record ScenarioDefinition
{
    /// <summary>Optional reference to the JSON schema, as written in the file.</summary>
    [JsonPropertyName("$schema")]
    public string? Schema { get; init; }

    /// <summary>Scenario key; equals the file name without extension.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Must be <see langword="true"/>. Marks every record produced by the scenario as simulated;
    /// the loader rejects a file where it is not.
    /// </summary>
    public required bool Synthetic { get; init; }

    /// <summary>Human-readable description of what the scenario demonstrates.</summary>
    public required string Description { get; init; }

    /// <summary>Identifier of the pathway definition the scenario runs against.</summary>
    public required string PathwayId { get; init; }

    /// <summary>Synthetic patients used by the steps; present on step-based scenarios.</summary>
    public IReadOnlyList<ScenarioPatient>? Patients { get; init; }

    /// <summary>The scripted events in file order; present on step-based scenarios.</summary>
    public IReadOnlyList<ScenarioStep>? Steps { get; init; }

    /// <summary>The cohort definition; present instead of <see cref="Patients"/> and <see cref="Steps"/> on cohort scenarios.</summary>
    public ScenarioCohort? Cohort { get; init; }

    /// <summary>Expected end state, asserted by journey and end-to-end tests.</summary>
    public ScenarioExpectation? Expect { get; init; }
}

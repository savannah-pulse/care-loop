using System.Text.Json.Serialization;

namespace MessagingSimulator.Scenarios;

/// <summary>
/// A scripted, synthetic sequence of events, mirroring the root object of <c>scenario.schema.json</c>.
/// </summary>
/// <remarks>
/// A scenario is either step-based (<see cref="Patients"/> and <see cref="Steps"/>) or a cohort
/// (<see cref="Cohort"/>). Clinical values in a scenario are placeholder codes from the pathway
/// definition; the simulator never invents clinical content.
/// </remarks>
public sealed record ScenarioDefinition
{
    /// <summary>Relative reference to the JSON schema the file declares (<c>$schema</c>), if any.</summary>
    [JsonPropertyName("$schema")]
    public string? Schema { get; init; }

    /// <summary>Scenario key; equals the file name without extension.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Marks every record produced by the scenario as simulated. The schema requires
    /// <see langword="true"/>; the loader must refuse a file where it is not.
    /// </summary>
    public required bool Synthetic { get; init; }

    /// <summary>Human-readable description of what the scenario demonstrates.</summary>
    public required string Description { get; init; }

    /// <summary>Identifier of the pathway definition the scenario runs against.</summary>
    public required string PathwayId { get; init; }

    /// <summary>Synthetic patients referenced by the steps; absent for a cohort scenario.</summary>
    public IReadOnlyList<ScenarioPatient>? Patients { get; init; }

    /// <summary>Ordered steps for both simulators; absent for a cohort scenario.</summary>
    public IReadOnlyList<ScenarioStep>? Steps { get; init; }

    /// <summary>Cohort definition; present only for a cohort scenario.</summary>
    public ScenarioCohort? Cohort { get; init; }

    /// <summary>What the journey and end-to-end tests assert after the scenario has run.</summary>
    public ScenarioExpectation? Expect { get; init; }
}

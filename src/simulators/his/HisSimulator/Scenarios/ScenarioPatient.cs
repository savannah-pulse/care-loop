namespace HisSimulator.Scenarios;

/// <summary>
/// A synthetic patient declared by a scenario file (<c>$defs/patient</c> in the schema).
/// </summary>
public sealed record ScenarioPatient
{
    /// <summary>Local handle that steps use in <see cref="ScenarioStep.PatientRef"/>.</summary>
    public required string Ref { get; init; }

    /// <summary>Synthetic patient identifier; always prefixed <c>SYN-</c>.</summary>
    public required string SyntheticId { get; init; }

    /// <summary>Visibly synthetic display name; always starts with <c>Synthetic</c>.</summary>
    public required string DisplayName { get; init; }

    /// <summary>BCP 47 language tag used to pick message templates.</summary>
    public required string Language { get; init; }

    /// <summary>
    /// How the patient reacts to notifications (<c>$defs/patientBehaviour</c>). Declared for the
    /// messaging simulator; the HIS simulator reads it only so the shared file parses and
    /// never acts on it.
    /// </summary>
    public System.Text.Json.JsonElement? Behaviour { get; init; }
}

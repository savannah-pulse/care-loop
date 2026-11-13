namespace PatientChannelSimulator.Scenarios;

/// <summary>
/// A synthetic patient declared by a scenario (<c>$defs/patient</c> in the schema).
/// </summary>
public sealed record ScenarioPatient
{
    /// <summary>Local handle that steps use to refer to the patient.</summary>
    public required string Ref { get; init; }

    /// <summary>Synthetic patient identifier; always prefixed <c>SYN-</c>.</summary>
    public required string SyntheticId { get; init; }

    /// <summary>Visibly synthetic display name; always starts with "Synthetic".</summary>
    public required string DisplayName { get; init; }

    /// <summary>BCP 47 language tag used to pick message templates.</summary>
    public required string Language { get; init; }

    /// <summary>How the patient reacts when CareLoop notifies them. This is the only part of a scenario the patient channel acts on.</summary>
    public required ScenarioPatientBehaviour Behaviour { get; init; }
}

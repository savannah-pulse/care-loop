namespace PatientChannelSimulator.State;

/// <summary>
/// The behaviour currently in force for one patient, as listed by <c>GET /api/patient-behaviours</c>.
/// </summary>
/// <param name="PatientSyntheticId">Synthetic identifier of the patient; always prefixed <c>SYN-</c>.</param>
/// <param name="Behaviour">How that patient reacts to notifications.</param>
/// <param name="ScenarioName">The scenario that set the behaviour, or <see langword="null"/> when it was set by hand.</param>
/// <param name="Simulated">Visible simulated-data marker; always <see langword="true"/>.</param>
public sealed record PatientBehaviourAssignment(
    string PatientSyntheticId,
    PatientBehaviour Behaviour,
    string? ScenarioName,
    bool Simulated = true);

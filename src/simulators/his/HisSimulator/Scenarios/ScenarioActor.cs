namespace HisSimulator.Scenarios;

/// <summary>
/// Which simulator performs a scenario step. Serialized as <c>his</c>.
/// </summary>
/// <remarks>
/// Only the HIS simulator performs steps. A patient's reactions are not steps: they are declared
/// as a behaviour on the scenario patient and applied by the messaging simulator when a
/// notification arrives. The field is kept so another system of record can be added later.
/// </remarks>
public enum ScenarioActor
{
    /// <summary>The HIS simulator: the actor whose steps this service runs.</summary>
    His,
}

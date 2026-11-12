namespace CareLoop.Domain.Cases;

public enum ActorKind
{
    /// <summary>An external system (HIS or patient-channel adapter).</summary>
    SourceSystem,

    /// <summary>An authorized person acting through the API.</summary>
    Human,

    /// <summary>The orchestrator itself; never accepted from external intake.</summary>
    System,
}

/// <summary>Who performed an action. Role names are demo placeholders until the identity model is agreed.</summary>
public sealed record EventActor(ActorKind Kind, string Id, string? Role = null)
{
    public const string OrchestratorId = "careloop-orchestrator";

    public static EventActor Orchestrator { get; } = new(ActorKind.System, OrchestratorId);

    public static EventActor ForSource(string sourceSystem) => new(ActorKind.SourceSystem, sourceSystem);
}

public static class ActorRoles
{
    public const string Coordinator = "coordinator";
    public const string FacilityLead = "facility-lead";
    public const string Clinician = "clinician";
}

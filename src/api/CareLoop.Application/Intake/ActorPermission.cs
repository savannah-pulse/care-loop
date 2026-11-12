using CareLoop.Domain.Cases;

namespace CareLoop.Application.Intake;

/// <summary>Which actors may submit an event type. Enforced by the intake pipeline, never by the UI.</summary>
public sealed class ActorPermission
{
    private readonly ActorKind kind;
    private readonly HashSet<string>? roles;

    private ActorPermission(ActorKind kind, IEnumerable<string>? roles)
    {
        this.kind = kind;
        this.roles = roles is null ? null : new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase);
    }

    public static ActorPermission SourceSystems { get; } = new(ActorKind.SourceSystem, null);

    public static ActorPermission Humans(params string[] roles) => new(ActorKind.Human, roles);

    public bool Allows(EventActor actor) =>
        actor.Kind == kind && (roles is null || (actor.Role is not null && roles.Contains(actor.Role)));
}

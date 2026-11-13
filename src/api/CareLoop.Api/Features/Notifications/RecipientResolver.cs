using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Events.DomainEvents;
using CareLoop.Domain.Recipients;

namespace CareLoop.Api.Features.Notifications;

/// <summary>
/// Works out who must be notified directly about a domain event, and how they can be reached.
/// </summary>
/// <remarks>
/// Will apply the routing table: <see cref="NotificationRequested"/> to the patient;
/// <see cref="HandoffProposed"/> to the proposed owner; <see cref="HandoffRejected"/>,
/// <see cref="ReferralRejected"/>, <see cref="CallRequested"/> and <see cref="DeadlineApproaching"/> to the
/// current owner; <see cref="EscalationRaised"/> to the facility lead and then the coordinator;
/// <see cref="CaseClosed"/> to nobody. The Domain holds no contact data, so patient contact points are
/// read from the stored screening-result event and staff are addressed through their simulator inbox.
/// </remarks>
public sealed class RecipientResolver
{
    private readonly CareLoopDbContext _db;

    /// <summary>
    /// Creates the resolver.
    /// </summary>
    /// <param name="db">Read access to stored source events, used to find patient contact points.</param>
    public RecipientResolver(CareLoopDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Resolves the recipients of a domain event.
    /// </summary>
    /// <param name="domainEvent">The event to route.</param>
    /// <param name="ct">Cancels the operation.</param>
    /// <returns>The recipients to notify; empty when the event has no direct notification.</returns>
    public Task<IReadOnlyList<Recipient>> ResolveAsync(IDomainEvent domainEvent, CancellationToken ct)
    {
        // TODO(WS-4): apply the routing table above and build Recipient records with contact points.
        throw new NotImplementedException("WS4: RecipientResolver.ResolveAsync");
    }

    /// <summary>
    /// Builds the staff recipient for a case owner.
    /// </summary>
    /// <param name="owner">The owner, role or queue to address.</param>
    /// <returns>A staff recipient reachable through the HIS simulator's staff inbox.</returns>
    public Recipient ForOwner(CaseOwner owner)
    {
        // TODO(WS-4): map the owner to a staff Recipient with a Simulator contact point.
        throw new NotImplementedException("WS4: RecipientResolver.ForOwner");
    }
}

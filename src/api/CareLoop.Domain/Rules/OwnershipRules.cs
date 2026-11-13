using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Time;

namespace CareLoop.Domain.Rules;

/// <summary>
/// Guards who owns a case and how ownership moves.
/// </summary>
/// <remarks>
/// Follow-up fails when everyone assumes someone else has the case. These rules guarantee exactly
/// one accountable owner at all times: transfer is propose-then-accept, the previous owner stays
/// accountable until acceptance, a rejection returns the next action to the current owner, and a
/// case nobody owns goes to the facility lead queue rather than to nobody.
/// </remarks>
public static class OwnershipRules
{
    /// <summary>
    /// Chooses the first owner of a newly opened case.
    /// </summary>
    /// <param name="facilityId">The facility that produced the result.</param>
    /// <param name="namedOwner">The responsible person named by the source, or <see langword="null"/> when none was named.</param>
    /// <param name="pathway">The pathway definition naming the unassigned queue role.</param>
    /// <returns>The named owner, or the facility lead queue of <paramref name="facilityId"/> when none was named.</returns>
    public static CaseOwner InitialOwner(string facilityId, Actor? namedOwner, PathwayDefinition pathway)
    {
        // TODO(WS-1): map the named actor to a CaseOwner, or build the facility lead queue owner (IsQueue = true).
        throw new NotImplementedException("WS1: OwnershipRules.InitialOwner");
    }

    /// <summary>
    /// Decides whether ownership may be proposed to a new owner, and builds the proposal.
    /// </summary>
    /// <param name="case">The case in its current state.</param>
    /// <param name="proposedOwner">The owner being asked to take the case.</param>
    /// <param name="proposedBy">Who is proposing; must be the current owner or an allowed role.</param>
    /// <param name="pathway">The pathway definition supplying the response deadline.</param>
    /// <param name="clock">Supplies the proposal time.</param>
    /// <param name="proposal">The proposal to pass to <see cref="Case.ProposeOwner"/> when allowed; otherwise <see langword="null"/>.</param>
    /// <returns>Whether the proposal is allowed and, if not, why.</returns>
    /// <remarks>Must refuse when the case is closed, when another proposal is pending, or when the proposer may not hand over this case.</remarks>
    public static RuleResult TryPropose(
        Case @case,
        CaseOwner proposedOwner,
        Actor proposedBy,
        PathwayDefinition pathway,
        IClock clock,
        out Assignment? proposal)
    {
        // TODO(WS-1): validate, then build an Assignment with RespondBy from DeadlinePolicy.
        throw new NotImplementedException("WS1: OwnershipRules.TryPropose");
    }

    /// <summary>
    /// Decides whether a response (acceptance or rejection) to the pending proposal is valid.
    /// </summary>
    /// <param name="case">The case in its current state.</param>
    /// <param name="respondedBy">Who is responding.</param>
    /// <returns>Whether the response is allowed and, if not, why.</returns>
    /// <remarks>Must refuse when no proposal is pending or when the responder is not the proposed owner: nobody can accept a case on another person's behalf.</remarks>
    public static RuleResult CanRespond(Case @case, Actor respondedBy)
    {
        // TODO(WS-1): check PendingAssignment exists and its ProposedOwner matches the responder.
        throw new NotImplementedException("WS1: OwnershipRules.CanRespond");
    }
}

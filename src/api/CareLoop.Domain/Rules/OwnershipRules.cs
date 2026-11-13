using CareLoop.Domain.Cases;
using CareLoop.Domain.Events;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Time;
using CareLoop.Domain.Tracks;

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
    /// Key of the next action a proposed owner must take, which names the response deadline in the pathway's deadlines.
    /// </summary>
    /// <remarks>
    /// A handoff is carried by a consultation, so the proposed owner's response deadline is the
    /// consultation's answer deadline, <see cref="ConsultationTrack.AnswerAction"/>. This is a key
    /// into the pathway definition, not a duration: how long the proposed owner has is configuration.
    /// </remarks>
    public const string RespondToHandoffAction = ConsultationTrack.AnswerAction;

    /// <summary>
    /// The role key under which CareLoop's own automation acts. It can never own a case.
    /// </summary>
    /// <remarks>A case always has a person or a staff queue accountable for it; automation is never accountable for a patient.</remarks>
    public const string SystemRole = "system";

    /// <summary>
    /// Chooses the first owner of a newly opened case.
    /// </summary>
    /// <param name="facilityId">The facility that produced the result.</param>
    /// <param name="namedOwner">The responsible person named by the source, or <see langword="null"/> when none was named.</param>
    /// <param name="pathway">The pathway definition naming the unassigned queue role.</param>
    /// <returns>The named owner, or the facility lead queue of <paramref name="facilityId"/> when none was named.</returns>
    /// <remarks>
    /// The queue owner's reference is <c>{queue role}:{facility}</c>, so every facility has one
    /// queue per pathway role and the dashboard can list the queue's cases by that reference.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="facilityId"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="pathway"/> is <see langword="null"/>.</exception>
    public static CaseOwner InitialOwner(string facilityId, Actor? namedOwner, PathwayDefinition pathway)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(facilityId);
        ArgumentNullException.ThrowIfNull(pathway);

        if (namedOwner is not null && !string.IsNullOrWhiteSpace(namedOwner.Reference))
        {
            return new CaseOwner
            {
                Reference = namedOwner.Reference,
                Role = namedOwner.Role,
                FacilityId = namedOwner.FacilityId ?? facilityId,
                IsQueue = false,
            };
        }

        return QueueOwner(facilityId, pathway);
    }

    /// <summary>
    /// Builds the facility lead queue owner for a facility.
    /// </summary>
    /// <param name="facilityId">The facility whose queue owns the case.</param>
    /// <param name="pathway">The pathway definition naming the unassigned queue role.</param>
    /// <returns>A queue owner (<see cref="CaseOwner.IsQueue"/> is <see langword="true"/>) for the facility.</returns>
    /// <remarks>Used for the first owner when nobody is named, and whenever a case must go back to the queue, so "nobody" is never the answer to "who is accountable?".</remarks>
    /// <exception cref="ArgumentException"><paramref name="facilityId"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="pathway"/> is <see langword="null"/>.</exception>
    public static CaseOwner QueueOwner(string facilityId, PathwayDefinition pathway)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(facilityId);
        ArgumentNullException.ThrowIfNull(pathway);

        return new CaseOwner
        {
            Reference = $"{pathway.UnassignedQueueRole}:{facilityId}",
            Role = pathway.UnassignedQueueRole,
            FacilityId = facilityId,
            IsQueue = true,
        };
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
    /// <remarks>
    /// <para>
    /// Refuses when the case is closed, when another proposal is pending, when the proposed owner is
    /// a queue (queues are assigned with <see cref="Case.AssignOwner"/>, not proposed to), when the
    /// proposed owner already owns the case, when the proposed owner's role is not one the pathway
    /// lets hold a case (see <see cref="MayOwn"/>), when the proposer may not hand over this case,
    /// or when the pathway defines no deadline for <see cref="RespondToHandoffAction"/>.
    /// </para>
    /// <para>
    /// The proposer may hand over the case when they are the current owner, when they hold the role
    /// of the queue that currently owns it (a facility lead picking a case from the queue), or when
    /// they hold a role the pathway allows to act in the case's current stage.
    /// </para>
    /// <para>
    /// The response deadline is read from the pathway's deadline for <see cref="RespondToHandoffAction"/>,
    /// preferring an urgency-specific entry, which is the same lookup <see cref="DeadlinePolicy.ComputeDueAt"/>
    /// performs. It is repeated here so this rule can be evaluated on its own.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public static RuleResult TryPropose(
        Case @case,
        CaseOwner proposedOwner,
        Actor proposedBy,
        PathwayDefinition pathway,
        IClock clock,
        out Assignment? proposal)
    {
        ArgumentNullException.ThrowIfNull(@case);
        ArgumentNullException.ThrowIfNull(proposedOwner);
        ArgumentNullException.ThrowIfNull(proposedBy);
        ArgumentNullException.ThrowIfNull(pathway);
        ArgumentNullException.ThrowIfNull(clock);

        proposal = null;

        if (@case.IsClosed)
        {
            return RuleResult.Deny("Case is closed; ownership of a closed case cannot be proposed.");
        }

        if (@case.PendingAssignment is not null)
        {
            return RuleResult.Deny(
                $"A handoff to '{@case.PendingAssignment.ProposedOwner.Reference}' is already pending and must be accepted or rejected first.");
        }

        if (proposedOwner.IsQueue)
        {
            return RuleResult.Deny("Ownership cannot be proposed to a queue; a queue is assigned directly.");
        }

        if (string.Equals(proposedOwner.Reference, @case.Owner.Reference, StringComparison.Ordinal))
        {
            return RuleResult.Deny($"'{proposedOwner.Reference}' already owns the case.");
        }

        if (!MayOwn(proposedOwner.Role, pathway))
        {
            return RuleResult.Deny(
                $"Role '{proposedOwner.Role}' may not own a case on pathway '{pathway.Id}'; owning roles are {string.Join(", ", OwnerRoles(pathway))}.");
        }

        if (!MayHandOver(@case, proposedBy, pathway))
        {
            return RuleResult.Deny(
                $"'{proposedBy.Reference}' ({proposedBy.Role}) may not hand over a case owned by '{@case.Owner.Reference}'.");
        }

        var deadline = FindDeadline(pathway, RespondToHandoffAction, @case.Urgency);
        if (deadline is null)
        {
            return RuleResult.Deny($"Pathway '{pathway.Id}' defines no deadline for '{RespondToHandoffAction}'.");
        }

        var now = clock.UtcNow;
        proposal = new Assignment
        {
            ProposedOwner = proposedOwner,
            ProposedBy = proposedBy,
            ProposedAt = now,
            RespondBy = now.Add(deadline.Duration),
            Status = AssignmentStatus.Proposed,
        };

        return RuleResult.Allow();
    }

    /// <summary>
    /// Decides whether a response (acceptance or rejection) to the pending proposal is valid.
    /// </summary>
    /// <param name="case">The case in its current state.</param>
    /// <param name="respondedBy">Who is responding.</param>
    /// <returns>Whether the response is allowed and, if not, why.</returns>
    /// <remarks>Refuses when the case is closed, when no proposal is pending or when the responder is not the proposed owner: nobody can accept a case on another person's behalf.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="case"/> or <paramref name="respondedBy"/> is <see langword="null"/>.</exception>
    public static RuleResult CanRespond(Case @case, Actor respondedBy)
    {
        ArgumentNullException.ThrowIfNull(@case);
        ArgumentNullException.ThrowIfNull(respondedBy);

        if (@case.IsClosed)
        {
            return RuleResult.Deny("Case is closed; there is no handoff to respond to.");
        }

        if (@case.PendingAssignment is null)
        {
            return RuleResult.Deny("No handoff is pending on this case.");
        }

        if (!string.Equals(@case.PendingAssignment.ProposedOwner.Reference, respondedBy.Reference, StringComparison.Ordinal))
        {
            return RuleResult.Deny(
                $"The handoff was proposed to '{@case.PendingAssignment.ProposedOwner.Reference}', not to '{respondedBy.Reference}'; nobody can respond on another person's behalf.");
        }

        return RuleResult.Allow();
    }

    /// <summary>
    /// Decides whether a role may hold ownership of a case on a pathway.
    /// </summary>
    /// <param name="role">The role key of the would-be owner.</param>
    /// <param name="pathway">The pathway definition naming the roles.</param>
    /// <returns><see langword="true"/> when the pathway names the role as an owner.</returns>
    /// <remarks>
    /// The owning roles are read from the pathway, never listed in code: every role named as a
    /// deadline's <see cref="DeadlineDefinition.OwnerRole"/>, as a stage's
    /// <see cref="StageDefinition.AllowedRoles"/> or as the <see cref="PathwayDefinition.UnassignedQueueRole"/>
    /// may own a case, so adding a persona such as a nurse navigator is a pathway edit. The
    /// <see cref="SystemRole"/> never may, whatever the pathway says, because automation is never
    /// accountable for a patient.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="pathway"/> is <see langword="null"/>.</exception>
    public static bool MayOwn(string role, PathwayDefinition pathway)
    {
        ArgumentNullException.ThrowIfNull(pathway);

        return !string.IsNullOrWhiteSpace(role)
            && !string.Equals(role, SystemRole, StringComparison.OrdinalIgnoreCase)
            && OwnerRoles(pathway).Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> OwnerRoles(PathwayDefinition pathway) =>
        pathway.Deadlines.Select(d => d.OwnerRole)
            .Concat(pathway.Stages.SelectMany(s => s.AllowedRoles))
            .Append(pathway.UnassignedQueueRole)
            .Where(r => !string.IsNullOrWhiteSpace(r) && !string.Equals(r, SystemRole, StringComparison.OrdinalIgnoreCase))
            .Select(r => r!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool MayHandOver(Case @case, Actor proposedBy, PathwayDefinition pathway)
    {
        if (string.Equals(proposedBy.Reference, @case.Owner.Reference, StringComparison.Ordinal))
        {
            return true;
        }

        if (@case.Owner.IsQueue && string.Equals(proposedBy.Role, @case.Owner.Role, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var stage = pathway.Stages.FirstOrDefault(s => s.Stage == @case.Stage);
        return stage is not null && stage.AllowedRoles.Contains(proposedBy.Role, StringComparer.OrdinalIgnoreCase);
    }

    private static DeadlineDefinition? FindDeadline(PathwayDefinition pathway, string nextAction, string? urgencyCode)
    {
        var candidates = pathway.Deadlines
            .Where(d => string.Equals(d.NextAction, nextAction, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return candidates.FirstOrDefault(d => urgencyCode is not null && string.Equals(d.UrgencyCode, urgencyCode, StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault(d => d.UrgencyCode is null);
    }
}

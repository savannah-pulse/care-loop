using CareLoop.Domain.Cases;
using CareLoop.Domain.Pathways;
using CareLoop.Domain.Tracks;

namespace CareLoop.Domain.Rules;

/// <summary>
/// Decides whether a case may be closed.
/// </summary>
/// <remarks>
/// Closure is the point at which CareLoop stops watching a patient. It is therefore allowed only
/// on an authorized human decision backed by evidence or a documented exception, so a case can
/// never be closed by automation, by AI or by an unsupported claim. A sent or accepted referral
/// is never evidence of completed care.
/// </remarks>
public static class ClosureGate
{
    /// <summary>
    /// The role key under which CareLoop's own automation acts. It can never authorize a closure.
    /// </summary>
    /// <remarks>Closure needs a human. Even a pathway that listed this role by mistake must not let the deadline worker, the AI workflow or any other automation close a case.</remarks>
    public const string SystemRole = "system";

    /// <summary>
    /// Evaluates a closure request against the pathway's evidence rules.
    /// </summary>
    /// <param name="case">The case the request applies to.</param>
    /// <param name="decision">The closure decision, including who authorized it and why.</param>
    /// <param name="pathway">The pathway definition supplying the evidence rules.</param>
    /// <returns>A result stating whether closure is allowed and, if not, the reason.</returns>
    /// <remarks>
    /// <para>Refuses when:</para>
    /// <list type="bullet">
    /// <item><description>the case is already closed;</description></item>
    /// <item><description>the reason has no rule in the pathway;</description></item>
    /// <item><description>the authorizer acts as <see cref="SystemRole"/>, or in a role the rule does not authorize;</description></item>
    /// <item><description>a required evidence reference, exception note, next surveillance date or number of documented attempts is missing;</description></item>
    /// <item><description>the reason is completed care (not an exception) but the evidence reference points at the referral, no outcome evidence has been recorded on the case, or a track the pathway requires for closure has not completed;</description></item>
    /// <item><description>the reason is <see cref="ClosureReason.TransferredAndAccepted"/> but the referral was not accepted by the receiving service. For that reason the acceptance itself is the evidence; for completed care a referral is never evidence.</description></item>
    /// </list>
    /// <para>The refusal reason is written to the audit trail by the caller. This method never mutates the case.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static RuleResult Evaluate(Case @case, ClosureDecision decision, PathwayDefinition pathway)
    {
        ArgumentNullException.ThrowIfNull(@case);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(pathway);

        if (@case.IsClosed)
        {
            return RuleResult.Deny($"Case is already closed ({@case.Closure?.Reason}); closure is final.");
        }

        var rule = pathway.ClosureRules.FirstOrDefault(r => r.Reason == decision.Reason);
        if (rule is null)
        {
            return RuleResult.Deny($"Pathway '{pathway.Id}' defines no closure rule for reason {decision.Reason}.");
        }

        var role = decision.AuthorizedBy.Role;
        if (string.IsNullOrWhiteSpace(role) || string.Equals(role, SystemRole, StringComparison.OrdinalIgnoreCase))
        {
            return RuleResult.Deny("Closure needs a human: the system role can never authorize a closure.");
        }

        if (!rule.AuthorizedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
        {
            return RuleResult.Deny(
                $"Role '{role}' is not authorized to close for reason {decision.Reason}; authorized roles are {string.Join(", ", rule.AuthorizedRoles)}.");
        }

        if (rule.RequiresEvidenceReference && string.IsNullOrWhiteSpace(decision.EvidenceReference))
        {
            return RuleResult.Deny($"Closure for reason {decision.Reason} requires an outcome evidence reference.");
        }

        if (rule.RequiresExceptionNote && string.IsNullOrWhiteSpace(decision.ExceptionNote))
        {
            return RuleResult.Deny($"Closure for reason {decision.Reason} requires a documented exception note.");
        }

        if (rule.RequiresNextSurveillanceDate && decision.NextSurveillanceDate is null)
        {
            return RuleResult.Deny($"Closure for reason {decision.Reason} requires a next surveillance date.");
        }

        if (decision.DocumentedAttemptReferences.Count < rule.MinimumDocumentedAttempts)
        {
            return RuleResult.Deny(
                $"Closure for reason {decision.Reason} requires at least {rule.MinimumDocumentedAttempts} documented contact attempt(s); {decision.DocumentedAttemptReferences.Count} given.");
        }

        if (decision.Reason == ClosureReason.TransferredAndAccepted && @case.Referral?.Status != ReferralStatus.Accepted)
        {
            return RuleResult.Deny(
                "Closure as transferred requires the referral to have been accepted by the receiving service; a referral that was only sent does not qualify.");
        }

        if (!rule.IsException)
        {
            if (IsReferralReference(@case, decision.EvidenceReference))
            {
                return RuleResult.Deny("A sent or accepted referral is not evidence of completed care; the evidence must be the recorded outcome.");
            }

            if (string.IsNullOrWhiteSpace(@case.OutcomeEvidenceReference))
            {
                return RuleResult.Deny(
                    $"Closure for reason {decision.Reason} is premature: no outcome has been recorded on the case.");
            }

            var incomplete = pathway.Tracks
                .Where(t => t.RequiredForClosure && !IsTrackComplete(@case, t.Kind))
                .Select(t => t.Kind.ToString())
                .ToList();

            if (incomplete.Count > 0)
            {
                return RuleResult.Deny(
                    $"Closure for reason {decision.Reason} requires these tracks to be complete: {string.Join(", ", incomplete)}.");
            }
        }

        return RuleResult.Allow();
    }

    private static bool IsReferralReference(Case @case, string? evidenceReference) =>
        !string.IsNullOrWhiteSpace(evidenceReference)
        && @case.Referral?.SourceReference is { } referral
        && string.Equals(referral, evidenceReference, StringComparison.Ordinal);

    private static bool IsTrackComplete(Case @case, TrackKind kind) => kind switch
    {
        TrackKind.Notification => @case.Notification?.IsComplete() ?? false,
        TrackKind.Consultation => @case.Consultation?.IsComplete() ?? false,
        TrackKind.Referral => @case.Referral?.IsComplete() ?? false,
        TrackKind.Appointment => @case.Appointment?.IsComplete() ?? false,
        _ => false,
    };
}

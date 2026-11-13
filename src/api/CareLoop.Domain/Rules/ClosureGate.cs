using CareLoop.Domain.Cases;
using CareLoop.Domain.Pathways;

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
    /// Evaluates a closure request against the pathway's evidence rules.
    /// </summary>
    /// <param name="case">The case the request applies to.</param>
    /// <param name="decision">The closure decision, including who authorized it and why.</param>
    /// <param name="pathway">The pathway definition supplying the evidence rules.</param>
    /// <returns>A result stating whether closure is allowed and, if not, the reason.</returns>
    /// <remarks>
    /// Must refuse when: the case is already closed; the authorizer's role is not in the rule's
    /// authorized roles; the reason has no rule in the pathway; a required evidence reference,
    /// exception note, next surveillance date or number of documented attempts is missing; or the
    /// only support offered is a sent or accepted referral. The refusal reason is audited by the caller.
    /// </remarks>
    public static RuleResult Evaluate(Case @case, ClosureDecision decision, PathwayDefinition pathway)
    {
        // TODO(WS-1): find the ClosureEvidenceRule for decision.Reason and check every requirement.
        throw new NotImplementedException("WS1: ClosureGate.Evaluate");
    }
}

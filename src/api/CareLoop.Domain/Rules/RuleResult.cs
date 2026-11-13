namespace CareLoop.Domain.Rules;

/// <summary>
/// The verdict of a guard: allowed, or refused with a reason that is written to the audit trail.
/// </summary>
/// <remarks>
/// Rules return a verdict instead of throwing so that a refused attempt (for example an
/// unsupported closure) can be audited as an ordinary, expected outcome.
/// </remarks>
public sealed record RuleResult
{
    /// <summary><see langword="true"/> when the rule allows the action.</summary>
    public required bool Allowed { get; init; }

    /// <summary>Why the action was refused; <see langword="null"/> when it is allowed.</summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Creates an allowing verdict.
    /// </summary>
    /// <returns>A result with <see cref="Allowed"/> set to <see langword="true"/>.</returns>
    public static RuleResult Allow() => new() { Allowed = true };

    /// <summary>
    /// Creates a refusing verdict.
    /// </summary>
    /// <param name="reason">Why the action is refused; recorded in the audit trail.</param>
    /// <returns>A result with <see cref="Allowed"/> set to <see langword="false"/> and the reason.</returns>
    public static RuleResult Deny(string reason) => new() { Allowed = false, Reason = reason };
}

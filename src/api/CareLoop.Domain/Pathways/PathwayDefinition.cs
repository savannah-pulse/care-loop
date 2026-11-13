namespace CareLoop.Domain.Pathways;

/// <summary>
/// The configuration that says how one kind of screening follow-up must run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Definition.</b> A pathway is the set of clinical and organisational decisions for one
/// follow-up process (for example breast-screening follow-up). The Domain rules are generic; the
/// pathway supplies the specifics. Every <see cref="CareLoop.Domain.Cases.Case"/> names the pathway
/// it follows.
/// </para>
/// <para><b>What a pathway defines.</b></para>
/// <list type="bullet">
/// <item><description><b>Urgency codes</b> (<see cref="UrgencyCodes"/>): which clinician-reviewed urgency values the hospital system may send.</description></item>
/// <item><description><b>Stages</b> (<see cref="Stages"/>): which event moves a case out of each stage, to which stage, which roles may cause it, and the next action on entry.</description></item>
/// <item><description><b>Tracks</b> (<see cref="Tracks"/>): which parallel tracks must be complete before closure, and the next action when one fails.</description></item>
/// <item><description><b>Deadlines</b> (<see cref="Deadlines"/>): how long each next action may take, optionally per urgency code, and when to warn the owner.</description></item>
/// <item><description><b>Escalation ladder</b> (<see cref="EscalationLadder"/>): who is told, and in what order, when a deadline passes.</description></item>
/// <item><description><b>Closure rules</b> (<see cref="ClosureRules"/>): for each closure reason, who may authorize it and what evidence or documented exception it needs.</description></item>
/// <item><description><b>Unassigned queue</b> (<see cref="UnassignedQueueRole"/>): the role that owns a case when no named person does.</description></item>
/// </list>
/// <para>
/// <b>Why it is configuration.</b> These are decisions for a clinical adviser, not for engineers.
/// Keeping them here means no deadline, urgency level or closure criterion is ever hard-coded in a
/// rule, and a second pathway can be added as another definition without code changes.
/// </para>
/// <para>
/// <b>Placeholder status.</b> Until a clinical adviser approves a definition,
/// <see cref="IsPlaceholder"/> is <see langword="true"/> and every value in it is a placeholder.
/// </para>
/// </remarks>
public sealed record PathwayDefinition
{
    /// <summary>Identifier of the pathway, for example <c>breast-screening</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Version of this definition, recorded so a decision can be traced to the rules in force.</summary>
    public required string Version { get; init; }

    /// <summary>Human-readable name of the pathway.</summary>
    public required string DisplayName { get; init; }

    /// <summary><see langword="true"/> while the values have not been approved by a clinical adviser.</summary>
    public required bool IsPlaceholder { get; init; }

    /// <summary>The urgency codes a clinical review may carry on this pathway. CareLoop only recognises them; it never chooses one.</summary>
    public required IReadOnlyList<string> UrgencyCodes { get; init; }

    /// <summary>Role key of the queue that owns a case nobody else owns.</summary>
    public required string UnassignedQueueRole { get; init; }

    /// <summary>Entry and exit rules for each stage.</summary>
    public required IReadOnlyList<StageDefinition> Stages { get; init; }

    /// <summary>The parallel tracks the pathway uses.</summary>
    public required IReadOnlyList<TrackDefinition> Tracks { get; init; }

    /// <summary>Every deadline the pathway imposes, by next action and urgency.</summary>
    public required IReadOnlyList<DeadlineDefinition> Deadlines { get; init; }

    /// <summary>Who is told, and when, once a deadline has passed.</summary>
    public required IReadOnlyList<EscalationStep> EscalationLadder { get; init; }

    /// <summary>What each closure reason requires.</summary>
    public required IReadOnlyList<ClosureEvidenceRule> ClosureRules { get; init; }
}

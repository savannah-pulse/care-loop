namespace CareLoop.Domain.Pathways;

/// <summary>
/// One kind of outcome evidence the pathway recognises, and what recording it means for the case.
/// </summary>
/// <remarks>
/// <para>
/// The design records a diagnosis, a treatment plan and the start of treatment as separate outcomes
/// (decision 20, C21). Which kinds exist is a clinical-governance decision, so the pathway declares
/// them and the intake refuses an outcome whose kind the pathway does not know: an unrecognised
/// outcome must never silently count as evidence.
/// </para>
/// <para>
/// A kind may name the next action that follows it (<see cref="NextAction"/>), for example
/// <c>start-treatment</c> after a treatment plan, so the case keeps a deadline for the step that
/// the design measures ("treatment started by the deadline"). The deadline itself is a
/// <see cref="DeadlineDefinition"/>; nothing here is a duration.
/// </para>
/// </remarks>
public sealed record EvidenceKindDefinition
{
    /// <summary>The code the outcome event carries, for example <c>treatment-plan</c>. Compared ignoring case.</summary>
    public required string Kind { get; init; }

    /// <summary>What the kind means, for the programme screen.</summary>
    public string? Description { get; init; }

    /// <summary><see langword="true"/> once a clinical adviser has approved the kind; <see langword="false"/> marks a placeholder.</summary>
    public required bool ClinicallyApproved { get; init; }

    /// <summary>
    /// Key of the next action to set when an outcome of this kind is recorded, or <see langword="null"/>
    /// to keep whatever next action the stage rules give the case.
    /// </summary>
    /// <remarks>Must match a <see cref="DeadlineDefinition.NextAction"/>, or the definition fails to load.</remarks>
    public string? NextAction { get; init; }
}

namespace CareLoop.Api.Infrastructure.Persistence.Audit;

/// <summary>
/// One staff read of case data: who looked, at what, when, and why if it was outside their team.
/// </summary>
/// <remarks>
/// <para>
/// The case store holds patient names and identifiers, so every look at it must be accountable (C4, decision 12),
/// and so must every search for a patient's other cases (C19). A read changes no case state, so it does not go
/// through <c>ICaseRepository.SaveAsync</c> and is not an <see cref="Domain.Events.AuditEvent"/>, which records a
/// source event and the projection it produced. Read slices add a record here through
/// <see cref="CareLoopDbContext.CaseReads"/> in the same request that serves the read.
/// </para>
/// <para>Append-only: a record is never updated or deleted by the application.</para>
/// </remarks>
public sealed class CaseReadRecord
{
    /// <summary>Unique identifier of this record.</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The case that was read; <see langword="null"/> for a list or search read, which names its query in
    /// <see cref="Query"/> instead. A list read that returns several cases may add one record per case.
    /// </summary>
    public Guid? CaseId { get; set; }

    /// <summary>Who read, as named by the caller's identity (today the demo headers).</summary>
    public string ActorReference { get; set; } = string.Empty;

    /// <summary>The role the reader acted in, for example <c>clinician</c>.</summary>
    public string ActorRole { get; set; } = string.Empty;

    /// <summary>The reader's facility, which decides whether the read was inside their team; <see langword="null"/> when the caller stated none.</summary>
    public string? ActorFacilityId { get; set; }

    /// <summary>What was read, as a short stable key such as <c>case</c>, <c>timeline</c> or <c>case-list</c>.</summary>
    public string View { get; set; } = string.Empty;

    /// <summary>
    /// The filters of a list or search read, for example <c>patientReference=SYN-PT-0001</c>; <see langword="null"/>
    /// for a single-case read.
    /// </summary>
    public string? Query { get; set; }

    /// <summary><see langword="true"/> when the case belongs to a facility other than the reader's.</summary>
    public bool OutsideTeam { get; set; }

    /// <summary>
    /// The reason the reader gave; required by the read endpoint when <see cref="OutsideTeam"/> is
    /// <see langword="true"/> (decision 12), otherwise usually <see langword="null"/>.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>When the read was served, from <c>IClock</c>.</summary>
    public DateTimeOffset ReadAt { get; set; }

    /// <summary>
    /// For a read made to ask an AI agent, the agent's name in the Foundry project, for example <c>role-briefing</c>;
    /// <see langword="null"/> for every other read.
    /// </summary>
    /// <remarks>Recorded with <see cref="AgentVersion"/>, <see cref="PromptVersion"/> and <see cref="CorrelationId"/> before the call, so the audit can say which agent version and which prompt wording wrote what a person read.</remarks>
    public string? AgentName { get; set; }

    /// <summary>For an AI read, the agent version CareLoop pinned and asked, for example <c>2</c>.</summary>
    public string? AgentVersion { get; set; }

    /// <summary>For an AI read, the version of the prompt's wording, for example <c>role-briefing-v2</c>.</summary>
    public string? PromptVersion { get; set; }

    /// <summary>For an AI read, the identifier of the call, also sent to the AI provider and written in CareLoop's logs.</summary>
    public Guid? CorrelationId { get; set; }
}

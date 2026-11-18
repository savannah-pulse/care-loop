namespace CareLoop.Contracts.Ai;

/// <summary>
/// Body of <c>POST /ai/role-briefing</c>: the cases the briefing is about.
/// </summary>
/// <remarks>
/// The dashboard sends the cases its "Start here today" card is about, the reader's "My cases", so the briefing and the
/// card describe the same cases. The API decides nothing about which cases are someone's own: it briefs those of the
/// named cases the reader may read in the worklist's scope and that are still open, and ignores the rest. The case
/// identifiers never reach the agent.
/// </remarks>
public sealed record RoleBriefingRequest
{
    /// <summary>The cases to brief on: from 1 to 1000, the most the dashboard's case list reads. A case named twice counts once.</summary>
    public required IReadOnlyList<Guid> CaseIds { get; init; }
}

using CareLoop.Domain.Cases;

namespace CareLoop.Api.Features.Summaries;

/// <summary>
/// The coordination facts given to an AI provider to draft a handoff summary.
/// </summary>
/// <remarks>
/// Contains references, statuses and timestamps only. It must never include patient names,
/// contact details or clinical narrative, so nothing sensitive leaves CareLoop in a prompt.
/// </remarks>
public sealed record HandoffSummaryRequest
{
    /// <summary>The case being handed over.</summary>
    public required Guid CaseId { get; init; }

    /// <summary>Identifier of the pathway the case follows.</summary>
    public required string PathwayId { get; init; }

    /// <summary>The stage the case is in.</summary>
    public required CaseStage Stage { get; init; }

    /// <summary>The clinician-reviewed urgency code, when reviewed. Given for context only; AI may not change it.</summary>
    public string? Urgency { get; init; }

    /// <summary>Role of the current owner.</summary>
    public required string CurrentOwnerRole { get; init; }

    /// <summary>Role of the proposed owner, when a handoff is pending.</summary>
    public string? ProposedOwnerRole { get; init; }

    /// <summary>Key of the next action.</summary>
    public string? NextAction { get; init; }

    /// <summary>When the next action is due.</summary>
    public DateTimeOffset? DueAt { get; init; }

    /// <summary>Names of the unresolved exceptions.</summary>
    public required IReadOnlyList<string> Exceptions { get; init; }

    /// <summary>The timeline as short factual lines, oldest first.</summary>
    public required IReadOnlyList<string> TimelineLines { get; init; }

    /// <summary>BCP 47 language tag the summary should be written in.</summary>
    public required string Language { get; init; }
}

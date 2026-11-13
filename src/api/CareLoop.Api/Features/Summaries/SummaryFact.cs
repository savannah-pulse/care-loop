using CareLoop.Domain.Events;

namespace CareLoop.Api.Features.Summaries;

/// <summary>
/// One finalized fact given to an AI provider, with the identifier the draft must cite when it uses it.
/// </summary>
/// <remarks>
/// Traceability is the point of this type. A drafted sentence is only usable if a reviewer can
/// follow it back to the record it came from, so every fact carries its own identifier, where it
/// came from and whether a person has reviewed it. A provider that returns a sentence citing an
/// identifier that was not supplied is returning something it invented, and the draft is refused.
/// </remarks>
public sealed record SummaryFact
{
    /// <summary>Identifier of this fact within the request; what a draft section cites.</summary>
    /// <remarks>Unique inside one request only. Derive it from the source event identifier so the same fact keeps the same identifier across drafts of the same case.</remarks>
    public required string FactId { get; init; }

    /// <summary>What kind of fact this is, for example <c>stage</c>, <c>deadline</c>, <c>exception</c>, <c>timeline-event</c>, <c>report-impression</c> or <c>recommended-next-step</c>.</summary>
    public required string FactType { get; init; }

    /// <summary>The fact itself, as the source recorded it.</summary>
    /// <remarks>
    /// May hold clinician-authored clinical text when <see cref="ReviewStatus"/> says a person
    /// finalized it (decision 9). Never a patient name or contact detail: those are not facts a
    /// handoff summary needs, and they must not leave CareLoop in a prompt.
    /// </remarks>
    public required string Value { get; init; }

    /// <summary>When the source recorded the fact.</summary>
    public required DateTimeOffset RecordedAt { get; init; }

    /// <summary>Reference to the record the fact came from, for example a <c>DiagnosticReport</c> reference or a CareLoop source event identifier.</summary>
    public required string SourceReference { get; init; }

    /// <summary>Whether a person has finalized this fact.</summary>
    /// <remarks>Only <see cref="FactReviewStatus.Finalized"/> facts may be sent. The field travels with the fact so a provider can state the status beside a value rather than implying it.</remarks>
    public required FactReviewStatus ReviewStatus { get; init; }

    /// <summary>The person the source names as having recorded or finalized the fact, when known.</summary>
    public Actor? RecordedBy { get; init; }
}

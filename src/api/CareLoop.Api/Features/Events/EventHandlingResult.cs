using CareLoop.Domain.Cases;

namespace CareLoop.Api.Features.Events;

/// <summary>
/// What an <see cref="IEventHandler"/> decided for one event.
/// </summary>
public sealed record EventHandlingResult
{
    /// <summary>The case after the event was applied, or unchanged when it was refused.</summary>
    public required Case Case { get; init; }

    /// <summary><see langword="true"/> when the event changed the case; <see langword="false"/> when a domain rule refused it.</summary>
    public required bool Applied { get; init; }

    /// <summary>The rule's refusal reason, written to the audit trail; <see langword="null"/> when applied.</summary>
    public string? RejectionReason { get; init; }

    /// <summary>
    /// What the applied event recorded, in plain words, written to the audit trail and shown on the case timeline;
    /// <see langword="null"/> when the event type alone says enough. Never carries patient detail.
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>Reference to the evidence the event carried, when it carried any.</summary>
    public string? EvidenceReference { get; init; }
}

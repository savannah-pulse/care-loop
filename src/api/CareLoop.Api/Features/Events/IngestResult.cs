namespace CareLoop.Api.Features.Events;

/// <summary>
/// The result of ingesting one event.
/// </summary>
public sealed record IngestResult
{
    /// <summary>What happened to the event.</summary>
    public required IngestOutcome Outcome { get; init; }

    /// <summary>The case the event belongs to; <see langword="null"/> when the event was invalid and no case was found.</summary>
    public Guid? CaseId { get; init; }

    /// <summary>Why the event was rejected or invalid; <see langword="null"/> otherwise.</summary>
    public string? Reason { get; init; }
}

using CareLoop.Domain.Tracks;

namespace CareLoop.Domain.Pathways;

/// <summary>
/// How a pathway uses one of the parallel tracks.
/// </summary>
public sealed record TrackDefinition
{
    /// <summary>The track being configured.</summary>
    public required TrackKind Kind { get; init; }

    /// <summary><see langword="true"/> when the track must be complete before closure on a non-exception reason.</summary>
    public required bool RequiredForClosure { get; init; }

    /// <summary>Key of the next action created when the track hits an exception (failed, rejected, missed).</summary>
    public required string NextActionOnException { get; init; }
}

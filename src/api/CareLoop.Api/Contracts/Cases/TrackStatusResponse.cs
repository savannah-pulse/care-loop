using CareLoop.Domain.Tracks;

namespace CareLoop.Contracts.Cases;

/// <summary>
/// The current state of one parallel track of a case.
/// </summary>
public sealed record TrackStatusResponse
{
    /// <summary>Which track this is.</summary>
    public required TrackKind Kind { get; init; }

    /// <summary>The track's status name, for example <c>Delivered</c> or <c>Accepted</c>; <see langword="null"/> when the track has not started.</summary>
    public string? Status { get; init; }

    /// <summary><see langword="true"/> when the track has reached its completing status. Delivery alone does not complete notification; an accepted referral is still not completed care.</summary>
    public required bool IsComplete { get; init; }

    /// <summary>When the track last changed; <see langword="null"/> when it has not started.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }
}

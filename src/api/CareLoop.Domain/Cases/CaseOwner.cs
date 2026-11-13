namespace CareLoop.Domain.Cases;

/// <summary>
/// The person or queue accountable for a case's next action.
/// </summary>
/// <remarks>
/// A case always has exactly one owner. When no named person owns it, the owner is the facility
/// lead queue (<see cref="IsQueue"/> is <see langword="true"/>), so "nobody" is never a valid answer
/// to "who is accountable?".
/// </remarks>
public sealed record CaseOwner
{
    /// <summary>Source-system reference of the person, or the queue identifier when <see cref="IsQueue"/> is <see langword="true"/>.</summary>
    public required string Reference { get; init; }

    /// <summary>The role key the owner holds, as named in the pathway definition.</summary>
    public required string Role { get; init; }

    /// <summary>The facility the owner belongs to.</summary>
    public required string FacilityId { get; init; }

    /// <summary><see langword="true"/> when the owner is a shared queue (the facility lead queue) rather than a named person.</summary>
    public bool IsQueue { get; init; }
}

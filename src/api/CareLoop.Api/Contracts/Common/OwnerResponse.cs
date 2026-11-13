namespace CareLoop.Contracts.Common;

/// <summary>
/// The person or queue accountable for a case, as shown in the dashboard.
/// </summary>
public sealed record OwnerResponse
{
    /// <summary>Source-system reference of the person, or the queue identifier.</summary>
    public required string Reference { get; init; }

    /// <summary>The role the owner holds.</summary>
    public required string Role { get; init; }

    /// <summary>The facility the owner belongs to.</summary>
    public required string FacilityId { get; init; }

    /// <summary><see langword="true"/> when the owner is the facility lead queue rather than a named person.</summary>
    public required bool IsQueue { get; init; }
}

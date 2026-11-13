namespace CareLoop.Domain.Cases;

/// <summary>
/// Where a proposed ownership transfer stands.
/// </summary>
public enum AssignmentStatus
{
    /// <summary>The transfer has been proposed and awaits a response. The previous owner is still accountable.</summary>
    Proposed = 0,

    /// <summary>The proposed owner accepted and is now accountable.</summary>
    Accepted = 1,

    /// <summary>The proposed owner refused; the next action returned to the current owner.</summary>
    Rejected = 2,
}

using HisSimulator.State;

namespace HisSimulator.Inbound;

/// <summary>
/// Body of <c>GET /staff-inbox</c>.
/// </summary>
/// <param name="Items">Staff inbox items, newest first.</param>
public sealed record StaffInboxResponse(IReadOnlyList<StaffInboxItem> Items)
{
    /// <summary>
    /// Always <see langword="true"/>: the inbox holds simulated messages only.
    /// </summary>
    public bool Synthetic => true;
}

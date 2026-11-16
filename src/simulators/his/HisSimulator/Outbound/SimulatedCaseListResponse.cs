using HisSimulator.State;

namespace HisSimulator.Outbound;

/// <summary>
/// Body of <c>GET /cases</c>: the simulated cases a clinician can act on.
/// </summary>
/// <param name="Cases">Every case the simulated HIS has sent a screening result for, in creation order.</param>
public sealed record SimulatedCaseListResponse(IReadOnlyList<SimulatedCaseItem> Cases)
{
    /// <summary>
    /// Always <see langword="true"/>: the list contains simulated cases only.
    /// </summary>
    public bool Synthetic => true;
}

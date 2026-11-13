namespace HisSimulator;

/// <summary>
/// Body of <c>GET /health</c>.
/// </summary>
/// <param name="Status">Liveness indicator; <c>ok</c> while the process is serving requests.</param>
/// <param name="Service">Stable service name, so a caller can tell the simulators apart.</param>
public sealed record HealthResponse(string Status, string Service)
{
    /// <summary>
    /// Always <see langword="true"/>: this service only ever handles simulated data.
    /// </summary>
    public bool Synthetic => true;
}

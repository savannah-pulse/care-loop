namespace MessagingSimulator;

/// <summary>
/// Body of <c>GET /health</c>, used by the Docker Compose health check.
/// </summary>
/// <param name="Status">Always <c>ok</c> while the process can answer requests.</param>
/// <param name="Service">Name of this service: <c>messaging-simulator</c>.</param>
/// <param name="Synthetic">Always <see langword="true"/>: this service only ever handles simulated data.</param>
public sealed record HealthResponse(string Status, string Service, bool Synthetic = true);

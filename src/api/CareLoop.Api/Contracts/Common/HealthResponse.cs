namespace CareLoop.Contracts.Common;

/// <summary>
/// Body of <c>GET /health</c>.
/// </summary>
public sealed record HealthResponse
{
    /// <summary>Always <c>ok</c> when the service is able to answer.</summary>
    public required string Status { get; init; }

    /// <summary>Name of the service that answered.</summary>
    public required string Service { get; init; }
}

namespace CareLoop.Contracts.Common;

/// <summary>
/// Body returned with HTTP 501 by endpoints that are scaffolded but not yet implemented.
/// </summary>
public sealed record NotImplementedResponse
{
    /// <summary>The workstream that will implement the endpoint, for example <c>WS6</c>.</summary>
    public required string Workstream { get; init; }

    /// <summary>The slice that is not implemented yet.</summary>
    public required string Feature { get; init; }

    /// <summary>A short explanation for whoever called the endpoint.</summary>
    public required string Message { get; init; }
}

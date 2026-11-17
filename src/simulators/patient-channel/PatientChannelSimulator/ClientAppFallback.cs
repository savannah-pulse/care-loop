namespace PatientChannelSimulator;

/// <summary>
/// Marks the endpoint that serves the ClientApp shell for client-side routes, and says which
/// paths must never be answered by it.
/// </summary>
/// <remarks>
/// A single-page app needs unknown paths to return its shell so a refresh on a client-side route
/// works. Paths under the host's own prefixes are different: there an unknown path is a wrong
/// address, and answering it with a page and 200 would make a caller believe the call succeeded.
/// </remarks>
public sealed class ClientAppFallback
{
    /// <summary>The marker put on the fallback endpoint.</summary>
    public static readonly ClientAppFallback Instance = new();

    private static readonly string[] HostPrefixes = ["/api", "/messages", "/scenarios", "/openapi", "/health"];

    private ClientAppFallback()
    {
    }

    /// <summary>
    /// Says whether a path belongs to the host's own routes rather than to the ClientApp.
    /// </summary>
    /// <param name="path">The request path.</param>
    /// <returns><see langword="true"/> when the path is one of the host's prefixes or lies under one.</returns>
    public static bool IsHostPath(PathString path) =>
        HostPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
}

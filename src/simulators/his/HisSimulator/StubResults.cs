using Microsoft.AspNetCore.Http.HttpResults;

namespace HisSimulator;

/// <summary>
/// Produces the response returned by every route that is scaffolded but not yet implemented.
/// </summary>
/// <remarks>
/// Scaffolded route handlers must not throw; they answer 501 so callers (CareLoop, the ClientApp,
/// Docker health probes) get a clear, well-formed signal. Remove each use as WS7 implements the route.
/// </remarks>
public static class StubResults
{
    /// <summary>
    /// Builds an RFC 9457 problem response with status 501 Not Implemented.
    /// </summary>
    /// <param name="handler">The handler that is not implemented yet, in the form <c>ClassName.MethodName</c>.</param>
    /// <returns>A problem result with status 501 naming the missing handler.</returns>
    public static ProblemHttpResult NotImplemented(string handler) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status501NotImplemented,
            title: "Not implemented",
            detail: $"WS7: {handler} (HIS simulator scaffold; synthetic data only)");
}

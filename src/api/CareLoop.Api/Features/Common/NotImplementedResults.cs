using CareLoop.Contracts.Common;

namespace CareLoop.Api.Features.Common;

/// <summary>
/// Builds the HTTP 501 answer returned by scaffolded endpoints until their workstream implements them.
/// </summary>
public static class NotImplementedResults
{
    /// <summary>
    /// Creates an HTTP 501 result naming the workstream that owns the endpoint.
    /// </summary>
    /// <param name="workstream">The owning workstream, for example <c>WS6</c>.</param>
    /// <param name="feature">The slice that is not implemented, for example <c>ListCases</c>.</param>
    /// <returns>A JSON result with status 501 and a <see cref="NotImplementedResponse"/> body.</returns>
    public static IResult For(string workstream, string feature) =>
        TypedResults.Json(
            new NotImplementedResponse
            {
                Workstream = workstream,
                Feature = feature,
                Message = $"{feature} is scaffolded but not implemented yet ({workstream}).",
            },
            statusCode: StatusCodes.Status501NotImplemented);
}

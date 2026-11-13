namespace PatientChannelSimulator;

/// <summary>
/// Body returned with <c>501 Not Implemented</c> by every route that is scaffolded but not built yet.
/// </summary>
/// <param name="Status">Always <c>not-implemented</c>.</param>
/// <param name="Route">The route that was called, as a method and template.</param>
/// <param name="Workstream">The workstream that owns the implementation (<c>WS8</c>).</param>
/// <param name="Synthetic">Visible simulated-data marker; always <see langword="true"/>.</param>
public sealed record NotImplementedResponse(string Status, string Route, string Workstream, bool Synthetic = true)
{
    /// <summary>
    /// Builds the 501 result for a scaffolded route.
    /// </summary>
    /// <param name="route">The route being answered, for example <c>GET /scenarios</c>.</param>
    /// <returns>A JSON result with status code 501 carrying a <see cref="NotImplementedResponse"/>.</returns>
    public static IResult For(string route) =>
        Results.Json(
            new NotImplementedResponse("not-implemented", route, "WS8"),
            statusCode: StatusCodes.Status501NotImplemented);
}

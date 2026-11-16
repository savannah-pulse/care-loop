using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace HisSimulator.Middleware;

/// <summary>Reports explicit simulator validation and upstream failures as Problem Details.</summary>
/// <param name="logger">Records rejected operations without raw FHIR bodies.</param>
internal sealed class SimulatorExceptionHandler(ILogger<SimulatorExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var status = exception switch
        {
            KeyNotFoundException or Scenarios.ScenarioNotFoundException => 404,
            ArgumentException or FormatException or InvalidDataException => 400,
            InvalidOperationException => 409, NotSupportedException => 422,
            HttpRequestException => 502, TaskCanceledException when !context.RequestAborted.IsCancellationRequested => 504,
            _ => 0,
        };
        if (status == 0) return false;
        logger.LogWarning("Simulator operation refused: {Type}, HTTP {Status}", exception.GetType().Name, status);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status, Title = "Simulator operation failed",
            Detail = status is 502 or 504 ? "CareLoop could not complete the FHIR exchange." : exception.Message,
        }, ct);
        return true;
    }
}

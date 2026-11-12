using Microsoft.AspNetCore.Diagnostics;

namespace CareLoop.Api;

/// <summary>Keeps client mistakes (such as a malformed JSON body) as 400 instead of letting them surface as 500.</summary>
public sealed class BadRequestExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException bad)
        {
            return false;
        }

        httpContext.Response.StatusCode = bad.StatusCode;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Status = bad.StatusCode, Title = "The request could not be read.", Detail = "The request body or parameters are malformed." },
        });
    }
}

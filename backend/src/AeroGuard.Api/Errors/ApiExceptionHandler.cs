using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AeroGuard.Api.Errors;

public sealed class ApiExceptionHandler(
    ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problem = exception as ApiProblemException;
        var statusCode = problem?.StatusCode ?? StatusCodes.Status500InternalServerError;

        if (problem is null)
        {
            logger.LogError(exception, "Unhandled API exception");
        }

        var details = new ProblemDetails
        {
            Status = statusCode,
            Title = problem?.Title ?? "The server could not complete the request",
            Detail = problem?.Message ?? "Check the server log for the failure reference.",
            Instance = httpContext.Request.Path,
        };
        details.Extensions["trace_id"] = httpContext.TraceIdentifier;
        if (problem?.Errors is not null)
        {
            details.Extensions["errors"] = problem.Errors;
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(details, cancellationToken);
        return true;
    }
}

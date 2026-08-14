using CareOps.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CareOps.Api.Middleware;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Access denied"),
            DomainException or InvalidOperationException => (StatusCodes.Status409Conflict, "Business rule conflict"),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrent update conflict"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected server error")
        };

        if (status >= 500) logger.LogError(exception, "Unhandled request failure");
        else logger.LogWarning(exception, "Request rejected with status {StatusCode}", status);

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status < 500 || environment.IsDevelopment() ? exception.Message : "An unexpected error occurred.",
            Instance = context.Request.Path,
            Extensions = { ["traceId"] = context.TraceIdentifier }
        }, cancellationToken);
        return true;
    }
}

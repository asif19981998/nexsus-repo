using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Nexus.BuildingBlocks.Exceptions.Handler;

/// <summary>Turns every unhandled exception into an RFC 7807 ProblemDetails body.</summary>
public class CustomExceptionHandler(ILogger<CustomExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            BadRequestException => StatusCodes.Status400BadRequest,
            NotFoundException => StatusCodes.Status404NotFound,
            UnauthorizedException => StatusCodes.Status401Unauthorized,
            ForbiddenException => StatusCodes.Status403Forbidden,
            ConflictException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled error on {Path}", context.Request.Path);
        else
            logger.LogInformation("{Type} on {Path}: {Message}", exception.GetType().Name, context.Request.Path, exception.Message);

        var problem = new ProblemDetails
        {
            Title = exception.GetType().Name,
            // Internal errors keep their details in the log, not in the response.
            Detail = status == StatusCodes.Status500InternalServerError ? "An unexpected error occurred." : exception.Message,
            Status = status,
            Instance = context.Request.Path,
        };
        problem.Extensions.Add("traceId", context.TraceIdentifier);

        if (exception is BadRequestException { Code: not null } bad)
        {
            problem.Extensions.Add("code", bad.Code);
            if (bad.Data_ is not null) problem.Extensions.Add("data", bad.Data_);
        }

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}

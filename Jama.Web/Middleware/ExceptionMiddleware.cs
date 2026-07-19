using Microsoft.AspNetCore.Mvc;

namespace Jama.Web.Middleware;

public sealed class ExceptionMiddleware(
    RequestDelegate next,
    ILogger<ExceptionMiddleware> logger,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogWarning(exception, "Unauthorized request to {Path}", context.Request.Path);
            await WriteProblem(context, StatusCodes.Status401Unauthorized, "Unauthorized",
                "A valid authenticated user is required.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}",
                context.Request.Method, context.Request.Path);
            var detail = environment.IsDevelopment()
                ? exception.Message
                : "An unexpected error occurred. Please retry or contact support.";
            await WriteProblem(context, StatusCodes.Status500InternalServerError,
                "Internal server error", detail);
        }
    }

    private static Task WriteProblem(HttpContext context, int status, string title, string detail)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path,
        });
    }
}

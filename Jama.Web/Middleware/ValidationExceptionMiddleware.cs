using FluentValidation;
using System.Net;
using System.Text.Json;

namespace Jama.Web.Middleware;

public class ValidationExceptionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";

            var payload = new
            {
                message = "Validation failed.",
                errors = ex.Errors.Select(e => e.ErrorMessage).ToArray(),
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
    }
}

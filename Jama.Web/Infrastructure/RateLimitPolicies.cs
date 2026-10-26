using System.Text.Json;
using System.Threading.RateLimiting;
using Jama.Application.Common.Models;
using Microsoft.AspNetCore.RateLimiting;

namespace Jama.Web.Infrastructure;

/// <summary>
/// Throttles for the few endpoints anyone on the internet can call without
/// signing in. Partitioned per client IP — real IPs, since
/// UseForwardedHeaders runs first and nginx supplies X-Forwarded-For.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>Public client sign-up. Each call creates an account and a row an
    /// admin has to look at, so a script hammering it would fill the review
    /// queue with junk; a real person registers once.</summary>
    public const string ClientRegister = "client-register";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static IServiceCollection AddJamaRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue<int?>("RateLimiting:ClientRegisterPermitLimit") ?? 10;
        var windowMinutes = configuration.GetValue<int?>("RateLimiting:ClientRegisterWindowMinutes") ?? 15;

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(ClientRegister, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromMinutes(windowMinutes),
                        QueueLimit = 0,
                    }));

            // Same envelope as every other failure, so the client shows this
            // sentence instead of a bare "request failed".
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.ContentType = "application/json";
                var payload = TypedResult<object>.Failure(
                    "Too many attempts from this network. Please wait a few minutes and try again.");
                await context.HttpContext.Response.WriteAsync(
                    JsonSerializer.Serialize(payload, JsonOptions), cancellationToken);
            };
        });
    }
}

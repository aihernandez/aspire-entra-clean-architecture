using System.Threading.RateLimiting;
using Microsoft.Identity.Web;

namespace Web.Api.Extensions;

internal static class RateLimitingExtensions
{
    internal static IServiceCollection AddRateLimitingInternal(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        int globalPermitLimit = configuration.GetValue<int?>("RateLimiting:Global:PermitLimit") ?? 100;
        int globalWindowSeconds = configuration.GetValue<int?>("RateLimiting:Global:WindowInSeconds") ?? 60;

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Without this, a rejected request gets a 429 with an empty body — every other
            // failure in this API is a ProblemDetails JSON body, so generated clients (Kiota,
            // ...) would otherwise have to special-case rate limiting alone.
            options.OnRejected = async (context, cancellationToken) =>
            {
                await Results.Problem(
                    type: "https://tools.ietf.org/html/rfc6585#section-4",
                    title: "Too many requests",
                    statusCode: StatusCodes.Status429TooManyRequests,
                    detail: "Too many requests. Please try again later.")
                    .ExecuteAsync(context.HttpContext);
            };

            // A global fixed-window limiter, partitioned by authenticated user or client IP.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetPartitionKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = globalPermitLimit,
                        Window = TimeSpan.FromSeconds(globalWindowSeconds)
                    }));

        });

        return services;
    }

    private static string GetPartitionKey(HttpContext httpContext)
    {
        if (httpContext.User.Identity?.IsAuthenticated == true &&
            Guid.TryParse(httpContext.User.GetTenantId(), out Guid tenantId) &&
            Guid.TryParse(httpContext.User.GetObjectId(), out Guid objectId))
        {
            return $"user:{tenantId:D}:{objectId:D}";
        }

        // Do not trust arbitrary X-Forwarded-For headers. Proxy forwarding requires a separately
        // configured trusted-proxy boundary. The quota is per API instance.
        return $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }
}

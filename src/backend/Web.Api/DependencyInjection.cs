using Web.Api.Infrastructure;

namespace Web.Api;

public static class DependencyInjection
{
    internal const string FrontendCorsPolicy = "Frontend";

    public static IServiceCollection AddPresentation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddEndpointsApiExplorer();

        services.AddControllersWithViews()
            .AddApplicationPart(typeof(global::Infrastructure.DependencyInjection).Assembly);

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        // Browser-based frontends (Angular, React, ...) can't use Aspire's server-side service
        // discovery, so they call Web.Api directly from their own origin — CORS has to allow that.
        // No AllowCredentials(): auth is a Bearer token in a header, not a cookie.
        string[] allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options =>
        {
            options.AddPolicy(FrontendCorsPolicy, policy =>
            {
                if (allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
                }
            });
        });

        return services;
    }
}

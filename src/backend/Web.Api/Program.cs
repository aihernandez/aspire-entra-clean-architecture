using System.Reflection;
using Application;
using HealthChecks.UI.Client;
using Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Web.Api;
using Web.Api.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOpenApiWithAuth();

builder.Services
    .AddApplication()
    .AddPresentation(builder.Configuration)
    .AddInfrastructure(builder.Configuration, builder.Environment);

builder.Services.AddRateLimitingInternal(builder.Configuration);

builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());

WebApplication app = builder.Build();

app.MapDefaultEndpoints();

app.MapEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseOpenApiWithUi();

    app.ApplyMigrations();

    // The detailed health report is useful locally, but should not expose dependencies publicly.
    app.MapHealthChecks("health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    }).AllowAnonymous().DisableRateLimiting();
}

app.MapHealthChecks("ready", new HealthCheckOptions
{
    Predicate = check => !check.Tags.Contains("live")
}).AllowAnonymous().DisableRateLimiting();

app.UseRequestContextLogging();

app.UseExceptionHandler();

app.UseCors(Web.Api.DependencyInjection.FrontendCorsPolicy);

app.UseAuthentication();

// Reject exhausted quotas and unauthorized principals before opening the user's SQL context.
app.UseRateLimiter();

app.UseAuthorization();

// Permission policies read claims only. Provision after they pass, before handlers need UserId.
app.UseUserProvisioning();

// REMARK: If you want to use Controllers, you'll need this.
app.MapControllers();

await app.RunAsync();

// REMARK: Required for functional and integration tests to work.
namespace Web.Api
{
    public partial class Program;
}

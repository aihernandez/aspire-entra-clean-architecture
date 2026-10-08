using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Web.Api.Extensions;

namespace Web.Api.Endpoints;

/// <summary>
/// Tells the browser how to sign in.
/// <para>
/// The SPA used to carry its own copy of the tenant id, its client id and the API scope in a
/// checked-in TypeScript file. That is a poor home for them in a template: whoever clones the
/// repository inherits somebody else's directory, and their app then authenticates against a tenant
/// they do not belong to. Serving the values from the API instead means **no tenant-specific value
/// lives in the frontend source at all** — one place to configure, and nothing to accidentally
/// commit.
/// </para>
/// <para>
/// Nothing here is a secret. The client id, tenant id and scope are all visible in the browser's
/// address bar the moment a sign-in redirect happens; they identify the application, they do not
/// authorize anything. What authorizes is the token Entra ID issues afterwards.
/// </para>
/// </summary>
internal sealed class AuthConfig : IEndpoint
{
    internal sealed record AuthConfigResponse(bool Enabled, string ClientId, string Authority, string ApiScope);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("auth-config", async Task<Results<Ok<AuthConfigResponse>, ProblemHttpResult>> (
            IConfiguration configuration,
            IAuthenticationSchemeProvider schemes) =>
        {
            AuthenticationScheme? activeScheme = await schemes.GetDefaultAuthenticateSchemeAsync();

            if (activeScheme?.Name == "Development")
            {
                return TypedResults.Ok(new AuthConfigResponse(false, string.Empty, string.Empty, string.Empty));
            }

            string? tenantId = configuration["AzureAd:TenantId"];
            string? apiClientId = configuration["AzureAd:ClientId"];
            string? spaClientId = configuration["AzureAd:SpaClientId"];
            string instance = configuration["AzureAd:Instance"] ?? "https://login.microsoftonline.com/";

            if (string.IsNullOrWhiteSpace(tenantId) ||
                string.IsNullOrWhiteSpace(apiClientId) ||
                string.IsNullOrWhiteSpace(spaClientId))
            {
                return TypedResults.Problem(
                    title: "Authentication is not configured",
                    detail: "Complete the Entra ID configuration before signing in.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            return TypedResults.Ok(new AuthConfigResponse(
                Enabled: true,
                ClientId: spaClientId,
                Authority: $"{instance.TrimEnd('/')}/{tenantId}",
                ApiScope: $"api://{apiClientId}/access_as_user"));
        })
        .Produces<AuthConfigResponse>()
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
        .AllowAnonymous()
        .WithTags(Tags.Users);
    }
}

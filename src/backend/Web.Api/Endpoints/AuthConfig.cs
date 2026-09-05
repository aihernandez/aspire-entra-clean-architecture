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
        app.MapGet("auth-config", Results<Ok<AuthConfigResponse>, EmptyHttpResult> (IConfiguration configuration) =>
        {
            string? tenantId = configuration["AzureAd:TenantId"];
            string? apiClientId = configuration["AzureAd:ClientId"];
            string? spaClientId = configuration["AzureAd:SpaClientId"];
            string instance = configuration["AzureAd:Instance"] ?? "https://login.microsoftonline.com/";

            // Not configured: the API is running on its development authentication scheme, so the
            // SPA must skip MSAL entirely rather than redirect to a tenant that isn't there.
            if (string.IsNullOrWhiteSpace(tenantId) ||
                string.IsNullOrWhiteSpace(apiClientId) ||
                string.IsNullOrWhiteSpace(spaClientId))
            {
                return TypedResults.Ok(new AuthConfigResponse(false, string.Empty, string.Empty, string.Empty));
            }

            return TypedResults.Ok(new AuthConfigResponse(
                Enabled: true,
                ClientId: spaClientId,
                Authority: $"{instance.TrimEnd('/')}/{tenantId}",
                ApiScope: $"api://{apiClientId}/access_as_user"));
        })
        .Produces<AuthConfigResponse>()
        .AllowAnonymous()
        .WithTags(Tags.Users);
    }
}

using Infrastructure.Authentication;
using Microsoft.AspNetCore.Builder;

namespace Infrastructure;

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Just-in-time user provisioning. Place it after <c>UseAuthentication</c> — it reads the
    /// validated principal — and before <c>UseAuthorization</c>, since it is what maps the token's
    /// (oid, tid) pair onto the local user record that every handler downstream depends on.
    /// </summary>
    public static IApplicationBuilder UseUserProvisioning(this IApplicationBuilder app)
    {
        app.UseMiddleware<UserProvisioningMiddleware>();

        return app;
    }
}

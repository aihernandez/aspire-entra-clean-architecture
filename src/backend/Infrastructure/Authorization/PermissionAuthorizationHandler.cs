using Infrastructure.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace Infrastructure.Authorization;

internal sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User is not { Identity.IsAuthenticated: true })
        {
            return Task.CompletedTask;
        }

        // Permissions come from the access token alone. This handler used to open a service scope
        // and ask the database whether the user held a role; doing that now would reintroduce the
        // local role store that Entra ID replaced.
        HashSet<string> permissions = PermissionProvider.GetForRoles(context.User.GetAppRoles());

        if (permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

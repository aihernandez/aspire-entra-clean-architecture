using SharedKernel;

namespace Infrastructure.Authorization;

/// <summary>
/// Maps Entra ID App Roles to this application's fine-grained permissions.
/// <para>
/// This is a pure function of the token's <c>roles</c> claim: no database, no directory call, no
/// <c>async</c>. That is the whole point of the design — role assignment lives in the cloud where a
/// tenant administrator manages it, while the mapping from a role to what it may actually do lives
/// in code, versioned alongside the code that enforces it (EntraIdMigration.md, D4/D5).
/// </para>
/// </summary>
internal static class PermissionProvider
{
    public static HashSet<string> GetForRoles(IReadOnlyCollection<string> roles)
    {
        var permissions = new HashSet<string>(StringComparer.Ordinal);

        bool isAdmin = roles.Contains(RoleNames.Admin);

        // Membership of this application is itself an App Role assignment. A token carrying neither
        // role belongs to somebody who authenticated against the tenant but was never assigned to
        // this application, and so gets nothing.
        if (isAdmin || roles.Contains(RoleNames.Member))
        {
            permissions.Add(PermissionNames.UsersAccess);
            permissions.Add(PermissionNames.TodosAccess);
        }

        if (isAdmin)
        {
            permissions.Add(PermissionNames.UsersReadAll);
        }

        return permissions;
    }
}

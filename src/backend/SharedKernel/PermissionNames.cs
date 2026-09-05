namespace SharedKernel;

/// <summary>
/// Fine-grained permissions enforced by <c>HasPermissionAttribute</c>. These are owned by the
/// application and versioned with the code that enforces them; they are derived from the App Roles
/// in the access token by <c>PermissionProvider</c> (see EntraIdMigration.md, D5).
/// </summary>
public static class PermissionNames
{
    /// <summary>Read one's own profile. Granted to every assigned user of the application.</summary>
    public const string UsersAccess = "users:access";

    /// <summary>List other people and read their profiles. Administrators only.</summary>
    public const string UsersReadAll = "users:read-all";

    /// <summary>Use the todo feature. Granted to every assigned user of the application.</summary>
    public const string TodosAccess = "todos:access";
}

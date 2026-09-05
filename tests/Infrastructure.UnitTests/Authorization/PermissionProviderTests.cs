using Infrastructure.Authorization;
using SharedKernel;

namespace Application.UnitTests.Authorization;

/// <summary>
/// The whole of this application's authorization reduces to this function. Every endpoint is gated
/// by a permission, and every permission comes from here — so a mistake in this mapping is not a
/// wrong screen, it is the wrong person seeing data.
/// </summary>
public sealed class PermissionProviderTests
{
    [Fact]
    public void GetForRoles_Should_GrantNothing_WhenNoAppRolePresent()
    {
        // Someone who authenticated against the tenant but was never assigned to this application.
        // This is the case that also covers every service principal registered in the directory,
        // which is why "valid token" must never be enough on its own.
        HashSet<string> permissions = PermissionProvider.GetForRoles([]);

        permissions.ShouldBeEmpty();
    }

    [Fact]
    public void GetForRoles_Should_GrantNothing_ForAnUnknownRole()
    {
        // A role that exists in the directory but that this application does not model. Silently
        // granting anything here would make the app's permissions depend on names a tenant
        // administrator can invent.
        HashSet<string> permissions = PermissionProvider.GetForRoles(["Contributor"]);

        permissions.ShouldBeEmpty();
    }

    [Fact]
    public void GetForRoles_Should_GrantBaselineAccess_ForMember()
    {
        HashSet<string> permissions = PermissionProvider.GetForRoles([RoleNames.Member]);

        permissions.ShouldBe([PermissionNames.UsersAccess, PermissionNames.TodosAccess], ignoreOrder: true);
    }

    [Fact]
    public void GetForRoles_Should_NotGrantUsersReadAll_ForMember()
    {
        // Reading other people is the one thing that separates the two roles. If this ever passes
        // by accident, every user can enumerate the organization.
        HashSet<string> permissions = PermissionProvider.GetForRoles([RoleNames.Member]);

        permissions.ShouldNotContain(PermissionNames.UsersReadAll);
    }

    [Fact]
    public void GetForRoles_Should_GrantEverything_ForAdmin()
    {
        HashSet<string> permissions = PermissionProvider.GetForRoles([RoleNames.Admin]);

        permissions.ShouldBe(
            [PermissionNames.UsersAccess, PermissionNames.TodosAccess, PermissionNames.UsersReadAll],
            ignoreOrder: true);
    }

    [Fact]
    public void GetForRoles_Should_ImplyMembership_ForAdminAlone()
    {
        // An administrator assigned only the Admin role must still be able to use the application.
        // Requiring both assignments would make a correct-looking directory configuration fail.
        HashSet<string> permissions = PermissionProvider.GetForRoles([RoleNames.Admin]);

        permissions.ShouldContain(PermissionNames.TodosAccess);
    }

    [Fact]
    public void GetForRoles_Should_BeCaseSensitive()
    {
        // Role values are a contract with the app registration manifest. Matching loosely would
        // hide a genuine misconfiguration until a different tenant spelled it another way.
        HashSet<string> permissions = PermissionProvider.GetForRoles(["admin"]);

        permissions.ShouldBeEmpty();
    }

    [Fact]
    public void GetForRoles_Should_Combine_WhenBothRolesPresent()
    {
        HashSet<string> permissions = PermissionProvider.GetForRoles([RoleNames.Member, RoleNames.Admin]);

        permissions.ShouldBe(
            [PermissionNames.UsersAccess, PermissionNames.TodosAccess, PermissionNames.UsersReadAll],
            ignoreOrder: true);
    }
}

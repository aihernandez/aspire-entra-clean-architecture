using System.Security.Claims;
using Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using SharedKernel;

namespace Infrastructure.UnitTests.Authorization;

public sealed class PermissionAuthorizationHandlerTests
{
    [Theory]
    [InlineData(RoleNames.Admin, PermissionNames.UsersReadAll, true)]
    [InlineData(RoleNames.Admin, PermissionNames.TodosAccess, true)]
    [InlineData(RoleNames.Member, PermissionNames.TodosAccess, true)]
    [InlineData(RoleNames.Member, PermissionNames.UsersReadAll, false)]
    [InlineData("Unknown", PermissionNames.TodosAccess, false)]
    [InlineData("admin", PermissionNames.UsersReadAll, false)]
    public async Task HandleAsync_Should_GrantOnlyPermissionsAssignedToRole(
        string role,
        string permission,
        bool expected)
    {
        ClaimsPrincipal principal = DelegatedPrincipal(role);

        AuthorizationHandlerContext context = await AuthorizeAsync(principal, permission);

        context.HasSucceeded.ShouldBe(expected);
        context.PendingRequirements.Any().ShouldBe(!expected);
    }

    [Fact]
    public async Task HandleAsync_Should_AcceptMappedIdentityScopeAndRoleClaims()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimConstants.ObjectId, Guid.NewGuid().ToString()),
            new Claim(ClaimConstants.TenantId, Guid.NewGuid().ToString()),
            new Claim("http://schemas.microsoft.com/identity/claims/scope", "other_scope access_as_user"),
            new Claim(ClaimTypes.Role, RoleNames.Admin)
        ], "Bearer"));

        AuthorizationHandlerContext context = await AuthorizeAsync(principal, PermissionNames.UsersReadAll);

        context.HasSucceeded.ShouldBeTrue();
        context.PendingRequirements.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("oid", null)]
    [InlineData("oid", "invalid-object-id")]
    [InlineData("tid", null)]
    [InlineData("tid", "invalid-tenant-id")]
    [InlineData("scp", null)]
    [InlineData("scp", "access_as_user_extra")]
    public async Task HandleAsync_Should_RejectAdmin_WhenRequiredClaimIsMissingOrInvalid(
        string claimType,
        string? replacement)
    {
        ClaimsPrincipal principal = DelegatedPrincipal(RoleNames.Admin);
        var identity = (ClaimsIdentity)principal.Identity!;
        identity.RemoveClaim(identity.FindFirst(claimType)!);
        if (replacement is not null)
        {
            identity.AddClaim(new Claim(claimType, replacement));
        }

        AuthorizationHandlerContext context = await AuthorizeAsync(principal, PermissionNames.UsersReadAll);

        context.HasSucceeded.ShouldBeFalse();
        context.PendingRequirements.ShouldHaveSingleItem().ShouldBeOfType<PermissionRequirement>();
    }

    [Theory]
    [InlineData("sub")]
    [InlineData(ClaimTypes.NameIdentifier)]
    [InlineData("idtyp")]
    public async Task HandleAsync_Should_RejectAppOnlyToken_WithAdminRoleAndDelegatedScope(string discriminator)
    {
        ClaimsPrincipal principal = DelegatedPrincipal(RoleNames.Admin);
        var identity = (ClaimsIdentity)principal.Identity!;
        string objectId = identity.FindFirst(ClaimConstants.Oid)!.Value;
        identity.AddClaim(new Claim(discriminator, discriminator == "idtyp" ? "app" : objectId));

        AuthorizationHandlerContext context = await AuthorizeAsync(principal, PermissionNames.UsersReadAll);

        context.HasSucceeded.ShouldBeFalse();
        context.PendingRequirements.ShouldHaveSingleItem().ShouldBeOfType<PermissionRequirement>();
    }

    [Fact]
    public async Task HandleAsync_Should_RejectUnauthenticatedPrincipal_WithAdminClaims()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(DelegatedPrincipal(RoleNames.Admin).Claims));

        AuthorizationHandlerContext context = await AuthorizeAsync(principal, PermissionNames.UsersReadAll);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_Should_RejectDelegatedUser_WithoutAssignedRole()
    {
        ClaimsPrincipal principal = DelegatedPrincipal(RoleNames.Member);
        var identity = (ClaimsIdentity)principal.Identity!;
        identity.RemoveClaim(identity.FindFirst("roles")!);

        AuthorizationHandlerContext context = await AuthorizeAsync(principal, PermissionNames.TodosAccess);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Theory]
    [InlineData(RoleNames.Admin, PermissionNames.UsersReadAll)]
    [InlineData(RoleNames.Member, PermissionNames.TodosAccess)]
    public async Task HandleAsync_Should_AllowDevelopmentUser_WithoutDelegatedScope(string role, string permission)
    {
        ClaimsPrincipal principal = DelegatedPrincipal(role, "Development");
        var identity = (ClaimsIdentity)principal.Identity!;
        identity.RemoveClaim(identity.FindFirst("scp")!);

        AuthorizationHandlerContext context = await AuthorizeAsync(principal, permission);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_Should_RejectDevelopmentAppOnlyToken_WithAdminRole()
    {
        ClaimsPrincipal principal = DelegatedPrincipal(RoleNames.Admin, "Development");
        var identity = (ClaimsIdentity)principal.Identity!;
        identity.RemoveClaim(identity.FindFirst("scp")!);
        identity.AddClaim(new Claim("idtyp", "app"));

        AuthorizationHandlerContext context = await AuthorizeAsync(principal, PermissionNames.UsersReadAll);

        context.HasSucceeded.ShouldBeFalse();
    }

    private static ClaimsPrincipal DelegatedPrincipal(string role, string authenticationType = "Bearer") =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimConstants.Oid, Guid.NewGuid().ToString()),
            new Claim(ClaimConstants.Tid, Guid.NewGuid().ToString()),
            new Claim("scp", "access_as_user"),
            new Claim("roles", role)
        ], authenticationType));

    private static async Task<AuthorizationHandlerContext> AuthorizeAsync(
        ClaimsPrincipal principal,
        string permission)
    {
        var requirement = new PermissionRequirement(permission);
        var context = new AuthorizationHandlerContext([requirement], principal, resource: null);
        var handler = new PermissionAuthorizationHandler();

        await handler.HandleAsync(context);

        return context;
    }
}

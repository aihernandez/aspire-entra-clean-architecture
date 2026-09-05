using System.Security.Claims;
using Infrastructure.Authentication;
using Microsoft.Identity.Web;

namespace Infrastructure.UnitTests.Authentication;

/// <summary>
/// Reading identity out of a token. Every one of these cases has a documented consequence: the
/// wrong claim silently keys data on a mutable value, and a missed app-only token hands a service
/// principal a user profile.
/// </summary>
public sealed class EntraClaimsTests
{
    private const string RolesClaimType = "roles";

    private static ClaimsPrincipal PrincipalWith(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "TestScheme"));

    [Fact]
    public void GetEntraObjectId_Should_ReadTheOidClaim()
    {
        var oid = Guid.NewGuid();
        ClaimsPrincipal principal = PrincipalWith(new Claim(ClaimConstants.Oid, oid.ToString()));

        principal.GetEntraObjectId().ShouldBe(oid);
    }

    [Fact]
    public void GetEntraObjectId_Should_ReadTheSchemaUriForm()
    {
        // ASP.NET Core renames inbound claims by default, so oid can arrive as a long schema URI.
        // A hand-rolled FindFirst("oid") works in one configuration and returns null in the other.
        var oid = Guid.NewGuid();
        ClaimsPrincipal principal = PrincipalWith(new Claim(ClaimConstants.ObjectId, oid.ToString()));

        principal.GetEntraObjectId().ShouldBe(oid);
    }

    [Fact]
    public void GetEntraObjectId_Should_ReturnNull_WhenAbsent()
    {
        PrincipalWith().GetEntraObjectId().ShouldBeNull();
    }

    [Fact]
    public void GetEntraTenantId_Should_ReadTheTidClaim()
    {
        var tid = Guid.NewGuid();
        ClaimsPrincipal principal = PrincipalWith(new Claim(ClaimConstants.Tid, tid.ToString()));

        principal.GetEntraTenantId().ShouldBe(tid);
    }

    [Fact]
    public void GetAppRoles_Should_ReadTheRolesClaim()
    {
        ClaimsPrincipal principal = PrincipalWith(
            new Claim(RolesClaimType, "Admin"),
            new Claim(RolesClaimType, "Member"));

        principal.GetAppRoles().ShouldBe(["Admin", "Member"], ignoreOrder: true);
    }

    [Fact]
    public void GetAppRoles_Should_ReadMappedRoleClaimsToo()
    {
        // Same claim, renamed by inbound mapping. Both forms have to work or authorization depends
        // on a framework default nobody set deliberately.
        ClaimsPrincipal principal = PrincipalWith(new Claim(ClaimTypes.Role, "Admin"));

        principal.GetAppRoles().ShouldBe(["Admin"]);
    }

    [Fact]
    public void GetAppRoles_Should_NotDuplicate_WhenBothFormsPresent()
    {
        ClaimsPrincipal principal = PrincipalWith(
            new Claim(RolesClaimType, "Admin"),
            new Claim(ClaimTypes.Role, "Admin"));

        principal.GetAppRoles().ShouldBe(["Admin"]);
    }

    [Fact]
    public void GetAppRoles_Should_BeEmpty_WhenNoRolesPresent()
    {
        PrincipalWith().GetAppRoles().ShouldBeEmpty();
    }

    [Fact]
    public void IsAppOnlyToken_Should_BeTrue_WhenOidEqualsSub()
    {
        // Microsoft's documented test for a token with no human behind it. Such a request must
        // never be given a user profile.
        string id = Guid.NewGuid().ToString();
        ClaimsPrincipal principal = PrincipalWith(
            new Claim(ClaimConstants.Oid, id),
            new Claim(ClaimConstants.Sub, id));

        principal.IsAppOnlyToken().ShouldBeTrue();
    }

    [Fact]
    public void IsAppOnlyToken_Should_BeFalse_ForADelegatedUserToken()
    {
        ClaimsPrincipal principal = PrincipalWith(
            new Claim(ClaimConstants.Oid, Guid.NewGuid().ToString()),
            new Claim(ClaimConstants.Sub, "pairwise-subject-value"));

        principal.IsAppOnlyToken().ShouldBeFalse();
    }

    [Fact]
    public void IsAppOnlyToken_Should_BeFalse_WhenEitherClaimIsMissing()
    {
        PrincipalWith(new Claim(ClaimConstants.Oid, Guid.NewGuid().ToString()))
            .IsAppOnlyToken()
            .ShouldBeFalse();
    }
}

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

    [Fact]
    public void IsAppOnlyToken_Should_ReadMappedSubjectAndObjectId()
    {
        string objectId = Guid.NewGuid().ToString();
        ClaimsPrincipal principal = PrincipalWith(
            new Claim(ClaimConstants.ObjectId, objectId),
            new Claim(ClaimTypes.NameIdentifier, objectId));

        principal.IsAppOnlyToken().ShouldBeTrue();
    }

    [Fact]
    public void IsAppOnlyToken_Should_RecognizeApplicationIdentityType_WithDifferentSubject()
    {
        ClaimsPrincipal principal = PrincipalWith(
            new Claim(ClaimConstants.Oid, Guid.NewGuid().ToString()),
            new Claim(ClaimConstants.Sub, "different-subject"),
            new Claim("idtyp", "app"));

        principal.IsAppOnlyToken().ShouldBeTrue();
    }

    [Theory]
    [InlineData("scp", "access_as_user")]
    [InlineData("scp", "other_scope access_as_user another_scope")]
    [InlineData("http://schemas.microsoft.com/identity/claims/scope", "access_as_user")]
    [InlineData("http://schemas.microsoft.com/identity/claims/scope", "other_scope access_as_user")]
    public void CanAccessUserEndpoints_Should_AcceptDelegatedScope_InRawOrMappedClaims(
        string scopeClaimType,
        string scopes)
    {
        ClaimsPrincipal principal = PrincipalWith(
            new Claim(ClaimConstants.Oid, Guid.NewGuid().ToString()),
            new Claim(ClaimConstants.Tid, Guid.NewGuid().ToString()),
            new Claim(scopeClaimType, scopes));

        principal.CanAccessUserEndpoints().ShouldBeTrue();
    }

    [Fact]
    public void CanAccessUserEndpoints_Should_AcceptMappedIdentityClaims()
    {
        ClaimsPrincipal principal = PrincipalWith(
            new Claim(ClaimConstants.ObjectId, Guid.NewGuid().ToString()),
            new Claim(ClaimConstants.TenantId, Guid.NewGuid().ToString()),
            new Claim("http://schemas.microsoft.com/identity/claims/scope", "access_as_user"));

        principal.CanAccessUserEndpoints().ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("other_scope")]
    [InlineData("access_as_user_extra")]
    [InlineData("prefix_access_as_user")]
    [InlineData("ACCESS_AS_USER")]
    public void CanAccessUserEndpoints_Should_RequireExactScope(string scopes)
    {
        ClaimsPrincipal principal = PrincipalWith(
            new Claim(ClaimConstants.Oid, Guid.NewGuid().ToString()),
            new Claim(ClaimConstants.Tid, Guid.NewGuid().ToString()),
            new Claim("scp", scopes));

        principal.CanAccessUserEndpoints().ShouldBeFalse();
    }

    [Theory]
    [InlineData("oid", null)]
    [InlineData("oid", "not-a-guid")]
    [InlineData("tid", null)]
    [InlineData("tid", "not-a-guid")]
    [InlineData("scp", null)]
    public void CanAccessUserEndpoints_Should_RejectMissingOrMalformedRequiredClaims(
        string claimType,
        string? replacement)
    {
        var claims = new List<Claim>
        {
            new(ClaimConstants.Oid, Guid.NewGuid().ToString()),
            new(ClaimConstants.Tid, Guid.NewGuid().ToString()),
            new("scp", "access_as_user")
        };
        claims.RemoveAll(claim => claim.Type == claimType);
        if (replacement is not null)
        {
            claims.Add(new Claim(claimType, replacement));
        }

        PrincipalWith([.. claims]).CanAccessUserEndpoints().ShouldBeFalse();
    }

    [Fact]
    public void CanAccessUserEndpoints_Should_RejectUnauthenticatedIdentity_WithValidClaims()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimConstants.Oid, Guid.NewGuid().ToString()),
            new Claim(ClaimConstants.Tid, Guid.NewGuid().ToString()),
            new Claim("scp", "access_as_user")
        ]));

        principal.CanAccessUserEndpoints().ShouldBeFalse();
    }

    [Theory]
    [InlineData("sub")]
    [InlineData(ClaimTypes.NameIdentifier)]
    [InlineData("idtyp")]
    public void CanAccessUserEndpoints_Should_RejectAppOnlyToken_EvenWithDelegatedScope(string discriminator)
    {
        string objectId = Guid.NewGuid().ToString();
        ClaimsPrincipal principal = PrincipalWith(
            new Claim(ClaimConstants.Oid, objectId),
            new Claim(ClaimConstants.Tid, Guid.NewGuid().ToString()),
            new Claim("scp", "access_as_user"),
            new Claim(discriminator, discriminator == "idtyp" ? "app" : objectId));

        principal.CanAccessUserEndpoints().ShouldBeFalse();
    }

    [Theory]
    [InlineData("Development", true)]
    [InlineData("development", false)]
    [InlineData("TestScheme", false)]
    public void CanAccessUserEndpoints_Should_AllowScopeException_OnlyForDevelopmentAuthentication(
        string authenticationType,
        bool expected)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimConstants.Oid, Guid.NewGuid().ToString()),
            new Claim(ClaimConstants.Tid, Guid.NewGuid().ToString())
        ], authenticationType));

        principal.CanAccessUserEndpoints().ShouldBe(expected);
    }

    [Fact]
    public void CanAccessUserEndpoints_Should_RejectDevelopmentAppOnlyToken()
    {
        string objectId = Guid.NewGuid().ToString();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimConstants.Oid, objectId),
            new Claim(ClaimConstants.Tid, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, objectId)
        ], "Development"));

        principal.CanAccessUserEndpoints().ShouldBeFalse();
    }

    [Theory]
    [InlineData(ClaimConstants.Oid)]
    [InlineData(ClaimConstants.Tid)]
    public void CanAccessUserEndpoints_Should_RequireValidIdentityClaims_ForDevelopmentAuthentication(string invalidClaimType)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimConstants.Oid, invalidClaimType == ClaimConstants.Oid ? "invalid" : Guid.NewGuid().ToString()),
            new Claim(ClaimConstants.Tid, invalidClaimType == ClaimConstants.Tid ? "invalid" : Guid.NewGuid().ToString())
        ], "Development"));

        principal.CanAccessUserEndpoints().ShouldBeFalse();
    }
}

using System.Security.Claims;
using Microsoft.Identity.Web;

namespace Infrastructure.Authentication;

/// <summary>
/// Reads the identity claims this application trusts. Everything here goes through
/// <c>Microsoft.Identity.Web</c>'s accessors rather than looking up literal claim names, because
/// ASP.NET Core renames inbound claims by default — <c>oid</c> arrives as a long schema URI unless
/// mapping is disabled, so a hand-rolled <c>FindFirst("oid")</c> works in some configurations and
/// silently returns null in others.
/// </summary>
internal static class EntraClaims
{
    private const string RolesClaimType = "roles";

    /// <summary>The <c>oid</c> claim: the user's immutable id within their tenant.</summary>
    public static Guid? GetEntraObjectId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.GetObjectId(), out Guid objectId) ? objectId : null;

    /// <summary>The <c>tid</c> claim: the tenant the caller belongs to.</summary>
    public static Guid? GetEntraTenantId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.GetTenantId(), out Guid tenantId) ? tenantId : null;

    /// <summary>
    /// App Roles from the <c>roles</c> claim. Both the raw claim type and the mapped
    /// <see cref="ClaimTypes.Role"/> are read, so the result is the same whether or not inbound
    /// claim mapping is on.
    /// </summary>
    public static IReadOnlyList<string> GetAppRoles(this ClaimsPrincipal principal) =>
    [
        .. principal
            .FindAll(claim => claim.Type is RolesClaimType or ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
    ];

    /// <summary>
    /// True when the token represents an application rather than a person. Microsoft's documented
    /// test is that <c>oid</c> and <c>sub</c> carry the same value in an app-only token.
    /// <para>
    /// This matters because validating the tenant and the presence of an <c>oid</c> is <i>not</i>
    /// the same as "somebody from our company": it would also admit every service principal
    /// registered in that tenant. Such requests must never be given a user profile.
    /// </para>
    /// </summary>
    public static bool IsAppOnlyToken(this ClaimsPrincipal principal)
    {
        string? objectId = principal.GetObjectId();
        string? subject = principal.FindFirstValue(ClaimConstants.Sub);

        return objectId is not null && subject is not null && string.Equals(objectId, subject, StringComparison.Ordinal);
    }
}

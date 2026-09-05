using Application.Abstractions.Authentication;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Authentication;

internal sealed class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    /// <summary>
    /// The local user id is resolved once per request by <see cref="UserProvisioningMiddleware"/>
    /// and stashed here, so handlers never pay a database round-trip to learn who is calling and
    /// the mapping from <c>oid</c> to the local key happens in exactly one place.
    /// </summary>
    internal const string LocalUserIdItemKey = "__LocalUserId";

    public Guid UserId =>
        httpContextAccessor.HttpContext?.Items[LocalUserIdItemKey] as Guid? ??
        throw new InvalidOperationException(
            "No local user is associated with this request. Endpoints that resolve the current " +
            "user must require a permission, which in turn requires an App Role — an app-only " +
            "token or an unauthenticated request reaches no such endpoint.");

    public Guid TenantId =>
        httpContextAccessor.HttpContext?.User.GetEntraTenantId() ??
        throw new InvalidOperationException("The request carries no tenant claim.");

    public IReadOnlyList<string> Roles =>
        httpContextAccessor.HttpContext?.User.GetAppRoles() ?? [];
}

using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Users.GetById;
using Infrastructure.Authorization;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

/// <summary>
/// The signed-in person's own profile.
/// <para>
/// This endpoint is a necessity under Entra ID rather than a convenience: the access token carries
/// the directory's <c>oid</c>, while every id in this application's own data is the local
/// <c>User.Id</c>. A client has no way to learn the latter except by asking.
/// </para>
/// </summary>
internal sealed class GetCurrent : IEndpoint
{
    internal sealed record CurrentUserResponse(
        Guid Id,
        string Email,
        string FirstName,
        string LastName,
        bool IsActive,
        string[] Roles,
        string[] Permissions);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("users/me", async (
            IUserContext userContext,
            IQueryHandler<GetUserByIdQuery, UserDetailResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetUserByIdQuery(userContext.UserId);

            Result<UserDetailResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match(
                profile => Results.Ok(new CurrentUserResponse(
                    profile.Id,
                    profile.Email,
                    profile.FirstName,
                    profile.LastName,
                    profile.IsActive,
                    [.. userContext.Roles.Order(StringComparer.Ordinal)],
                    [.. PermissionProvider.GetForRoles(userContext.Roles).Order(StringComparer.Ordinal)])),
                CustomResults.Problem);
        })
        .Produces<CurrentUserResponse>()
        .ProducesProblemResponses()
        .HasPermission(PermissionNames.UsersAccess)
        .WithTags(Tags.Users);
    }
}

using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Users.GetById;
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
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("users/me", async (
            IUserContext userContext,
            IQueryHandler<GetUserByIdQuery, UserDetailResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetUserByIdQuery(userContext.UserId);

            Result<UserDetailResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .Produces<UserDetailResponse>()
        .ProducesProblemResponses()
        .HasPermission(PermissionNames.UsersAccess)
        .WithTags(Tags.Users);
    }
}

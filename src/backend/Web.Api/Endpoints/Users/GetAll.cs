using Application.Abstractions.Messaging;
using Application.Users.GetAll;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

internal sealed class GetAll : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("users", async (
            IQueryHandler<GetUsersQuery, PagedResponse<UserResponse>> handler,
            CancellationToken cancellationToken,
            int pageNumber = 1,
            int pageSize = 20) =>
        {
            var query = new GetUsersQuery(pageNumber, pageSize);

            Result<PagedResponse<UserResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .Produces<PagedResponse<UserResponse>>()
        .ProducesProblemResponses()
        .HasPermission(PermissionNames.UsersReadAll)
        .WithTags(Tags.Users);
    }
}

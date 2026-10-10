using Application.Abstractions.Messaging;
using Application.Users.UpdateStatus;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

/// <summary>Removes local access while keeping the row referenced by historical records.</summary>
internal sealed class Delete : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("users/{userId:guid}", async (
            Guid userId,
            ICommandHandler<UpdateUserStatusCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new UpdateUserStatusCommand(userId, false), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblemResponses()
        .HasPermission(PermissionNames.UsersManage)
        .WithTags(Tags.Users);
    }
}

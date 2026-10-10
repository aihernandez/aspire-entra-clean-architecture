using Application.Abstractions.Messaging;
using Application.Users.UpdateStatus;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

internal sealed class UpdateStatus : IEndpoint
{
    internal sealed record Request
    {
        public required bool IsActive { get; init; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("users/{userId:guid}/status", async (
            Guid userId,
            Request request,
            ICommandHandler<UpdateUserStatusCommand> handler,
            CancellationToken cancellationToken) =>
        {
            Result result = await handler.Handle(
                new UpdateUserStatusCommand(userId, request.IsActive), cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblemResponses()
        .HasPermission(PermissionNames.UsersManage)
        .WithTags(Tags.Users);
    }
}

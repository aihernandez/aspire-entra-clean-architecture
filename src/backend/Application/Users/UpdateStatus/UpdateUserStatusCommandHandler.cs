using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.UpdateStatus;

internal sealed class UpdateUserStatusCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IDateTimeProvider clock) : ICommandHandler<UpdateUserStatusCommand>
{
    public async Task<Result> Handle(UpdateUserStatusCommand command, CancellationToken cancellationToken)
    {
        if (!userContext.Roles.Contains(RoleNames.Admin))
        {
            return Result.Failure(UserErrors.Unauthorized());
        }

        if (!command.IsActive && command.UserId == userContext.UserId)
        {
            return Result.Failure(UserErrors.CannotDeactivateSelf);
        }

        User? user = await context.Users.SingleOrDefaultAsync(
            candidate => candidate.Id == command.UserId &&
                         candidate.EntraTenantId == userContext.TenantId,
            cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(command.UserId));
        }

        if (user.IsActive == command.IsActive)
        {
            return Result.Success();
        }

        user.IsActive = command.IsActive;
        user.DeactivatedAtUtc = command.IsActive ? null : clock.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

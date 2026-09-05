using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.GetById;

internal sealed class GetUserByIdQueryHandler(IApplicationDbContext context, IUserContext userContext)
    : IQueryHandler<GetUserByIdQuery, UserDetailResponse>
{
    public async Task<Result<UserDetailResponse>> Handle(GetUserByIdQuery query, CancellationToken cancellationToken)
    {
        // Reading somebody else's profile is an administrator action. The check reads the App Roles
        // from the token rather than any local role store — that is the whole point of D4.
        if (query.UserId != userContext.UserId && !userContext.Roles.Contains(RoleNames.Admin))
        {
            return Result.Failure<UserDetailResponse>(UserErrors.Unauthorized());
        }

        UserDetailResponse? user = await context.Users
            .AsNoTracking()
            .Where(user => user.Id == query.UserId)
            .Select(user => new UserDetailResponse
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                IsActive = user.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return Result.Failure<UserDetailResponse>(UserErrors.NotFound(query.UserId));
        }

        return user;
    }
}

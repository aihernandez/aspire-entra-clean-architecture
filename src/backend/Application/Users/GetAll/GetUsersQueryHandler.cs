using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.GetAll;

/// <summary>
/// Lists people from the application's own mirror table, which by construction contains only those
/// who have signed in at least once (there is no directory-wide enumeration without Microsoft
/// Graph — see EntraIdMigration.md, D10). The UI is expected to say so.
/// </summary>
internal sealed class GetUsersQueryHandler(IApplicationDbContext context, IUserContext userContext)
    : IQueryHandler<GetUsersQuery, PagedResponse<UserResponse>>
{
    private const int MaxPageSize = 100;

    public async Task<Result<PagedResponse<UserResponse>>> Handle(
        GetUsersQuery query,
        CancellationToken cancellationToken)
    {
        long offset = ((long)query.PageNumber - 1) * query.PageSize;
        if (query.PageNumber < 1 || query.PageSize is < 1 or > MaxPageSize || offset > int.MaxValue)
        {
            return Result.Failure<PagedResponse<UserResponse>>(UserErrors.InvalidPagination);
        }

        IQueryable<Domain.Users.User> users = context.Users
            .AsNoTracking()
            .Where(user => user.EntraTenantId == userContext.TenantId);
        if (!query.IncludeInactive)
        {
            users = users.Where(user => user.IsActive);
        }

        int totalCount = await users.CountAsync(cancellationToken);

        List<UserResponse> items = await users
            .OrderBy(user => user.FirstName)
            .ThenBy(user => user.LastName)
            .ThenBy(user => user.Id)
            .Skip((int)offset)
            .Take(query.PageSize)
            .Select(user => new UserResponse
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                IsActive = user.IsActive
            })
            .ToListAsync(cancellationToken);

        return new PagedResponse<UserResponse>(items, query.PageNumber, query.PageSize, totalCount);
    }
}

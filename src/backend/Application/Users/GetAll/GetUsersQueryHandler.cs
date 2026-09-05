using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.GetAll;

/// <summary>
/// Lists people from the application's own mirror table, which by construction contains only those
/// who have signed in at least once (there is no directory-wide enumeration without Microsoft
/// Graph — see EntraIdMigration.md, D10). The UI is expected to say so.
/// </summary>
internal sealed class GetUsersQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetUsersQuery, PagedResponse<UserResponse>>
{
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 20;

    public async Task<Result<PagedResponse<UserResponse>>> Handle(
        GetUsersQuery query,
        CancellationToken cancellationToken)
    {
        int pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        int pageSize = query.PageSize is < 1 or > MaxPageSize ? DefaultPageSize : query.PageSize;

        IQueryable<Domain.Users.User> users = context.Users.AsNoTracking().Where(user => user.IsActive);

        int totalCount = await users.CountAsync(cancellationToken);

        List<UserResponse> items = await users
            .OrderBy(user => user.FirstName)
            .ThenBy(user => user.LastName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new UserResponse
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName
            })
            .ToListAsync(cancellationToken);

        return new PagedResponse<UserResponse>(items, pageNumber, pageSize, totalCount);
    }
}

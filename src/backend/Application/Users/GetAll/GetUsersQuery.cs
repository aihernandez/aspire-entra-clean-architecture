using Application.Abstractions.Messaging;

namespace Application.Users.GetAll;

public sealed record GetUsersQuery(int PageNumber = 1, int PageSize = 20) : IQuery<PagedResponse<UserResponse>>;

namespace Application.Users.GetById;

public sealed record UserDetailResponse
{
    public Guid Id { get; init; }

    public string Email { get; init; }

    public string FirstName { get; init; }

    public string LastName { get; init; }

    public bool IsActive { get; init; }
}

using SharedKernel;

namespace Domain.Users;

public static class UserErrors
{
    public static readonly Error InvalidPagination = new(
        "Users.InvalidPagination",
        "Page number must be positive, page size must be between 1 and 100, and the offset must not exceed Int32.MaxValue.",
        ErrorType.Validation);

    public static Error NotFound(Guid userId) => Error.NotFound(
        "Users.NotFound",
        $"The user with the Id = '{userId}' was not found");

    public static Error Unauthorized() => Error.Forbidden(
        "Users.Unauthorized",
        "You are not authorized to perform this action.");

    public static readonly Error Deactivated = Error.Forbidden(
        "Users.Deactivated",
        "This account has been deactivated.");
}

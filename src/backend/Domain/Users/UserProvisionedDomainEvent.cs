using SharedKernel;

namespace Domain.Users;

/// <summary>
/// Raised the first time a person authenticates and their local record is created just-in-time.
/// With Entra ID there is no registration step, so this is the closest thing the application has
/// to "an account came into existence".
/// </summary>
public sealed record UserProvisionedDomainEvent(Guid UserId) : IDomainEvent;

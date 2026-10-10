using Application.Abstractions.Messaging;

namespace Application.Users.UpdateStatus;

public sealed record UpdateUserStatusCommand(Guid UserId, bool IsActive) : ICommand;

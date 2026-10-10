using FluentValidation;

namespace Application.Users.UpdateStatus;

internal sealed class UpdateUserStatusCommandValidator : AbstractValidator<UpdateUserStatusCommand>
{
    public UpdateUserStatusCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
    }
}

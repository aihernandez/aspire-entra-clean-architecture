using Application.Abstractions.Data;
using Application.Abstractions.Email;
using Application.Abstractions.Email.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Users.Provisioned;

/// <summary>
/// Greets a person the first time they sign in. Under Entra ID there is no registration to hang a
/// welcome message on, so first provisioning is the moment the account genuinely begins to exist
/// from the application's point of view.
/// </summary>
internal sealed class SendWelcomeEmailOnUserProvisioned(
    IApplicationDbContext context,
    IEmailService emailService,
    IEmailTemplate<WelcomeEmailModel> template,
    ILogger<SendWelcomeEmailOnUserProvisioned> logger)
    : IDomainEventHandler<Domain.Users.UserProvisionedDomainEvent>
{
    public async Task Handle(
        Domain.Users.UserProvisionedDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        var recipient = await context.Users
            .AsNoTracking()
            .Where(user => user.Id == domainEvent.UserId)
            .Select(user => new { user.Email, user.FirstName, user.LastName })
            .FirstOrDefaultAsync(cancellationToken);

        if (recipient is null || recipient.Email.Length == 0)
        {
            // A tenant that emits no email claim is unusual but legal. Not a reason to fail the
            // request that triggered provisioning.
            logger.LogWarning(
                "No email address available for user {UserId}; welcome message skipped.",
                domainEvent.UserId);

            return;
        }

        await emailService.SendAsync(
            recipient.Email,
            $"{recipient.FirstName} {recipient.LastName}".Trim(),
            template,
            new WelcomeEmailModel(recipient.FirstName),
            cancellationToken);
    }
}

using Application.Abstractions.Email;
using Application.Abstractions.Email.Models;
using Application.UnitTests.Abstractions;
using Application.Users.Provisioned;
using Domain.Users;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.UnitTests.Users;

public sealed class SendWelcomeEmailOnUserProvisionedTests : BaseHandlerTest
{
    [Fact]
    public async Task DeliveryFailure_Should_NotFailAnAlreadySavedUser()
    {
        await using TestDbContext context = CreateDbContext();
        var user = new User { Id = Guid.NewGuid(), Email = "ada@example.com", FirstName = "Ada", LastName = "Lovelace" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        IEmailService<WelcomeEmailModel> sender = Substitute.For<IEmailService<WelcomeEmailModel>>();
        sender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<WelcomeEmailModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("SMTP unavailable")));
        var handler = new SendWelcomeEmailOnUserProvisioned(
            context, sender, NullLogger<SendWelcomeEmailOnUserProvisioned>.Instance);

        await Should.NotThrowAsync(() => handler.Handle(new UserProvisionedDomainEvent(user.Id), CancellationToken.None));
        (await context.Users.FindAsync(user.Id)).ShouldNotBeNull();
    }

    [Fact]
    public async Task SuccessfulDelivery_Should_SendToTheProvisionedUser()
    {
        await using TestDbContext context = CreateDbContext();
        var user = new User { Id = Guid.NewGuid(), Email = "ada@example.com", FirstName = "Ada", LastName = "Lovelace" };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        IEmailService<WelcomeEmailModel> sender = Substitute.For<IEmailService<WelcomeEmailModel>>();
        var handler = new SendWelcomeEmailOnUserProvisioned(
            context, sender, NullLogger<SendWelcomeEmailOnUserProvisioned>.Instance);

        await handler.Handle(new UserProvisionedDomainEvent(user.Id), CancellationToken.None);

        await sender.Received(1).SendAsync(
            "ada@example.com", "Ada Lovelace", Arg.Any<WelcomeEmailModel>(), Arg.Any<CancellationToken>());
    }
}

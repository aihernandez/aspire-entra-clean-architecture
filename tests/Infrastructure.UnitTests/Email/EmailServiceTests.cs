using Application.Abstractions.Email;
using Application.Abstractions.Email.Models;
using Infrastructure.Email;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infrastructure.UnitTests.Email;

public sealed class EmailServiceTests
{
    [Fact]
    public async Task SendAsync_Should_RenderTheRegisteredTemplateAndSendItsCompleteResult()
    {
        IEmailSender sender = Substitute.For<IEmailSender>();
        IEmailTemplate<WelcomeEmailModel> template = Substitute.For<IEmailTemplate<WelcomeEmailModel>>();
        var sut = new EmailService<WelcomeEmailModel>(
            sender,
            template,
            NullLogger<EmailService<WelcomeEmailModel>>.Instance);
        var model = new WelcomeEmailModel("Ada");
        var renderedEmail = new RenderedEmail("A subject", "<p>A body</p>");
        using var cancellation = new CancellationTokenSource();

        template.RenderAsync(model, cancellation.Token).Returns(Task.FromResult(renderedEmail));

        await sut.SendAsync("ada@example.com", "Ada Lovelace", model, cancellation.Token);

        await sender.Received(1).SendAsync(
            Arg.Is<EmailMessage>(message =>
                message.ToEmail == "ada@example.com" &&
                message.ToName == "Ada Lovelace" &&
                message.Subject == renderedEmail.Subject &&
                message.HtmlBody == renderedEmail.HtmlBody),
            cancellation.Token);
    }

    [Fact]
    public async Task SendAsync_Should_NotCallTheTransport_WhenRenderingFails()
    {
        IEmailSender sender = Substitute.For<IEmailSender>();
        IEmailTemplate<WelcomeEmailModel> template = Substitute.For<IEmailTemplate<WelcomeEmailModel>>();
        var sut = new EmailService<WelcomeEmailModel>(
            sender,
            template,
            NullLogger<EmailService<WelcomeEmailModel>>.Instance);
        var model = new WelcomeEmailModel("Ada");

        template.RenderAsync(model, Arg.Any<CancellationToken>())
            .Returns<Task<RenderedEmail>>(_ => throw new InvalidOperationException("Rendering failed."));

        await sut.SendAsync("ada@example.com", "Ada Lovelace", model);

        await sender.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
    }
}

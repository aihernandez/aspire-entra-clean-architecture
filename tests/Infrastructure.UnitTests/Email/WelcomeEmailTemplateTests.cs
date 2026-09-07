using Application.Abstractions.Email;
using Application.Abstractions.Email.Models;
using Infrastructure.Email;
using Infrastructure.Email.Templates;
using Microsoft.Extensions.Options;

namespace Infrastructure.UnitTests.Email;

public sealed class WelcomeEmailTemplateTests
{
    [Fact]
    public async Task RenderAsync_Should_OwnTheSubjectAndRenderedBody()
    {
        var renderer = new StubEmailBodyRenderer("<p>Welcome, Ada!</p>");
        IOptions<EmailBrandingOptions> branding = Options.Create(new EmailBrandingOptions
        {
            AppName = "Contoso"
        });
        var sut = new WelcomeEmailTemplate(renderer, branding);
        var model = new WelcomeEmailModel("Ada");
        using var cancellation = new CancellationTokenSource();

        RenderedEmail result = await sut.RenderAsync(model, cancellation.Token);

        result.Subject.ShouldBe("Welcome to Contoso!");
        result.HtmlBody.ShouldBe("<p>Welcome, Ada!</p>");
        renderer.ReceivedModel.ShouldBe(model);
        renderer.ReceivedCancellationToken.ShouldBe(cancellation.Token);
    }

    private sealed class StubEmailBodyRenderer(string htmlBody) : IEmailBodyRenderer<WelcomeEmailModel>
    {
        public WelcomeEmailModel? ReceivedModel { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<string> RenderAsync(
            WelcomeEmailModel model,
            CancellationToken cancellationToken = default)
        {
            ReceivedModel = model;
            ReceivedCancellationToken = cancellationToken;

            return Task.FromResult(htmlBody);
        }
    }
}

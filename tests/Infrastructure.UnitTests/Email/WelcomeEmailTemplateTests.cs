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
        var renderer = new StubRazorEmailRenderer("<p>Welcome, Ada!</p>");
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
        renderer.ReceivedPath.ShouldBe("/Views/Emails/WelcomeEmail.cshtml");
        renderer.ReceivedModel.ShouldBe(model);
        renderer.ReceivedCancellationToken.ShouldBe(cancellation.Token);
    }

    private sealed class StubRazorEmailRenderer(string htmlBody) : IRazorEmailRenderer
    {
        public string? ReceivedPath { get; private set; }

        public object? ReceivedModel { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<string> RenderAsync<TModel>(
            string viewPath,
            TModel model,
            CancellationToken cancellationToken = default)
            where TModel : notnull
        {
            ReceivedPath = viewPath;
            ReceivedModel = model;
            ReceivedCancellationToken = cancellationToken;

            return Task.FromResult(htmlBody);
        }
    }
}

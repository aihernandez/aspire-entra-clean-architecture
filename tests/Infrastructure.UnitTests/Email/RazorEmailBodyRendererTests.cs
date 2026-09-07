using Application.Abstractions.Email.Models;
using Infrastructure.Email;
using Infrastructure.Email.Templates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Infrastructure.UnitTests.Email;

public sealed class RazorEmailBodyRendererTests
{
    [Fact]
    public async Task RenderAsync_Should_RenderTheTypedModelAndSharedLayout()
    {
        var branding = new EmailBrandingOptions
        {
            AppName = "Contoso",
            SupportEmail = "help@contoso.example",
            PrimaryColor = "#123456"
        };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IOptions<EmailBrandingOptions>>(Options.Create(branding));
        services.AddSingleton<IDateTimeProvider>(new StubDateTimeProvider(
            new DateTime(2042, 1, 2, 3, 4, 5, DateTimeKind.Utc)));

        await using ServiceProvider serviceProvider = services.BuildServiceProvider();
        ILoggerFactory loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var sut = new RazorEmailBodyRenderer<WelcomeEmail, WelcomeEmailModel>(
            serviceProvider,
            loggerFactory);

        string html = await sut.RenderAsync(new WelcomeEmailModel("<Ada>"));

        html.ShouldContain("<html lang=\"en\">");
        html.ShouldContain("Welcome, &lt;Ada&gt;!");
        html.ShouldNotContain("Welcome, <Ada>!");
        html.ShouldContain("Contoso");
        html.ShouldContain("#123456");
        html.ShouldContain("help@contoso.example");
        html.ShouldContain("2042");
    }

    private sealed class StubDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}

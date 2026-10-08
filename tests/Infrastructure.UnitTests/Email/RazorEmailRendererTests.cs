using Application.Abstractions.Email.Models;
using Infrastructure.Email;
using Infrastructure.Email.Templates;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Infrastructure.UnitTests.Email;

public sealed class RazorEmailRendererTests
{
    [Fact]
    public async Task RenderAsync_Should_RenderTypedModelAndSharedLayout()
    {
        await using WebApplication app = CreateApplication();
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        IRazorEmailRenderer renderer = scope.ServiceProvider.GetRequiredService<IRazorEmailRenderer>();

        string html = await renderer.RenderAsync(
            "/Views/Emails/WelcomeEmail.cshtml",
            new WelcomeEmailModel("<Ada>"));

        html.ShouldContain("<html lang=\"en\">");
        html.ShouldContain("Welcome, &lt;Ada&gt;!");
        html.ShouldNotContain("Welcome, <Ada>!");
        html.ShouldContain("Contoso");
        html.ShouldContain("#123456");
        html.ShouldContain("help@contoso.example");
        html.ShouldContain("2042");
    }

    [Fact]
    public async Task RenderAsync_Should_RejectUnknownView()
    {
        await using WebApplication app = CreateApplication();
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        IRazorEmailRenderer renderer = scope.ServiceProvider.GetRequiredService<IRazorEmailRenderer>();

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            renderer.RenderAsync("/Views/Emails/Missing.cshtml", new WelcomeEmailModel("Ada")));

        exception.Message.ShouldContain("/Views/Emails/Missing.cshtml");
    }

    private static WebApplication CreateApplication()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        IServiceCollection services = builder.Services;
        services.AddLogging();
        services.AddControllersWithViews()
            .AddApplicationPart(typeof(WelcomeEmailTemplate).Assembly);
        services.AddScoped<IRazorEmailRenderer, RazorEmailRenderer>();
        services.AddSingleton<IOptions<EmailBrandingOptions>>(Options.Create(new EmailBrandingOptions
        {
            AppName = "Contoso",
            SupportEmail = "help@contoso.example",
            PrimaryColor = "#123456"
        }));
        services.AddSingleton<IDateTimeProvider>(new StubDateTimeProvider(
            new DateTime(2042, 1, 2, 3, 4, 5, DateTimeKind.Utc)));

        return builder.Build();
    }

    private sealed class StubDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}

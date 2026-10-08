using Application.Abstractions.Email;
using Application.Abstractions.Email.Models;
using Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Web.Api;

namespace ArchitectureTests.Email;

public sealed class EmailTemplateWiringTests
{
    [Fact]
    public async Task WelcomeEmail_Should_RenderFromTheApiServiceRegistration()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] =
                "Server=localhost;Database=unused;User Id=sa;Password=unused;TrustServerCertificate=True",
            ["Email:Branding:AppName"] = "Contoso"
        });
        builder.Services
            .AddPresentation(builder.Configuration)
            .AddInfrastructure(builder.Configuration, builder.Environment);

        await using WebApplication app = builder.Build();
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        IEmailTemplate<WelcomeEmailModel> template = scope.ServiceProvider
            .GetRequiredService<IEmailTemplate<WelcomeEmailModel>>();

        RenderedEmail email = await template.RenderAsync(new WelcomeEmailModel("<Ada>"));

        email.Subject.ShouldBe("Welcome to Contoso!");
        email.HtmlBody.ShouldContain("<html lang=\"en\">");
        email.HtmlBody.ShouldContain("Welcome, &lt;Ada&gt;!");
        email.HtmlBody.ShouldNotContain("Welcome, <Ada>!");
    }
}

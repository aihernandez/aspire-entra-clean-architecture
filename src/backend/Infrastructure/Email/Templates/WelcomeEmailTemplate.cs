using Application.Abstractions.Email;
using Application.Abstractions.Email.Models;
using Microsoft.Extensions.Options;

namespace Infrastructure.Email.Templates;

internal sealed class WelcomeEmailTemplate(EmailTemplateEngine engine, IOptions<EmailBrandingOptions> branding)
    : IEmailTemplate<WelcomeEmailModel>
{
    public string Subject => $"Welcome to {branding.Value.AppName}!";

    public Task<string> RenderAsync(WelcomeEmailModel model, CancellationToken cancellationToken = default) =>
        Task.FromResult(engine.Render("Welcome", model));
}

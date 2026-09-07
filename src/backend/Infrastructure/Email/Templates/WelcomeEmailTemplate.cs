using Application.Abstractions.Email;
using Application.Abstractions.Email.Models;
using Microsoft.Extensions.Options;

namespace Infrastructure.Email.Templates;

internal sealed class WelcomeEmailTemplate(
    IEmailBodyRenderer<WelcomeEmailModel> renderer,
    IOptions<EmailBrandingOptions> branding)
    : IEmailTemplate<WelcomeEmailModel>
{
    public async Task<RenderedEmail> RenderAsync(
        WelcomeEmailModel model,
        CancellationToken cancellationToken = default)
    {
        string htmlBody = await renderer.RenderAsync(model, cancellationToken);

        return new RenderedEmail($"Welcome to {branding.Value.AppName}!", htmlBody);
    }
}

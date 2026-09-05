using Application.Abstractions.Email;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Email;

/// <summary>
/// Renders the given template and hands it to the configured <see cref="IEmailSender"/>. Email is
/// a best-effort side effect, never the reason a request fails: any exception from rendering or
/// sending is logged and swallowed here, once, so every caller (registration, password reset, ...)
/// gets that resilience for free instead of having to remember to wrap each call.
/// </summary>
internal sealed class EmailService(IEmailSender sender, ILogger<EmailService> logger) : IEmailService
{
    public async Task SendAsync<TModel>(
        string toEmail,
        string toName,
        IEmailTemplate<TModel> template,
        TModel model,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string html = await template.RenderAsync(model, cancellationToken);

            await sender.SendAsync(new EmailMessage(toEmail, toName, template.Subject, html), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to send email with subject '{Subject}' to {ToEmail}.",
                template.Subject,
                toEmail);
        }
    }
}

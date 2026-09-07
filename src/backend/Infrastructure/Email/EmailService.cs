using Application.Abstractions.Email;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Email;

/// <summary>
/// Renders the given template and hands it to the configured <see cref="IEmailSender"/>. Email is
/// a best-effort side effect, never the reason a request fails: any exception from rendering or
/// sending is logged and swallowed here, once, so every caller (registration, password reset, ...)
/// gets that resilience for free instead of having to remember to wrap each call.
/// </summary>
internal sealed class EmailService<TModel>(
    IEmailSender sender,
    IEmailTemplate<TModel> template,
    ILogger<EmailService<TModel>> logger) : IEmailService<TModel>
    where TModel : notnull
{
    public async Task SendAsync(
        string toEmail,
        string toName,
        TModel model,
        CancellationToken cancellationToken = default)
    {
        try
        {
            RenderedEmail renderedEmail = await template.RenderAsync(model, cancellationToken);

            await sender.SendAsync(
                new EmailMessage(toEmail, toName, renderedEmail.Subject, renderedEmail.HtmlBody),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to render or send {EmailModel} email to {ToEmail}.",
                typeof(TModel).Name,
                toEmail);
        }
    }
}

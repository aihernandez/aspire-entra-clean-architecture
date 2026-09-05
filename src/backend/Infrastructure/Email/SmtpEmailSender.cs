using Application.Abstractions.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Infrastructure.Email;

/// <summary>
/// Real SMTP delivery via MailKit. Used both in Development (pointed at the local MailPit
/// container) and in Production (pointed at a real provider) — only the configured host/port/
/// credentials differ, not the code path.
/// </summary>
internal sealed class SmtpEmailSender(
    IOptions<EmailOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        EmailOptions smtp = options.Value;

        using var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(smtp.FromName, smtp.FromEmail));
        mimeMessage.To.Add(new MailboxAddress(message.ToName, message.ToEmail));
        mimeMessage.Subject = message.Subject;
        mimeMessage.Body = new BodyBuilder { HtmlBody = message.HtmlBody }.ToMessageBody();

        using var client = new SmtpClient();

        SecureSocketOptions socketOptions = smtp.UseSsl
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTlsWhenAvailable;

        await client.ConnectAsync(smtp.Host, smtp.Port, socketOptions, cancellationToken);

        if (!string.IsNullOrWhiteSpace(smtp.Username))
        {
            await client.AuthenticateAsync(smtp.Username, smtp.Password, cancellationToken);
        }

        await client.SendAsync(mimeMessage, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        logger.LogInformation(
            "Sent email '{Subject}' to {ToEmail} via SMTP {Host}:{Port}.",
            message.Subject,
            message.ToEmail,
            smtp.Host,
            smtp.Port);
    }
}

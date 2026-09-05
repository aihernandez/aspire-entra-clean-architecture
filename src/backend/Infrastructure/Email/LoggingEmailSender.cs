using Application.Abstractions.Email;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Email;

/// <summary>
/// Fallback transport used when no SMTP server is configured at all (no Aspire "mailpit"
/// connection string and no Smtp:Host) — so a fresh clone of the template still runs end to end
/// instead of throwing on the first registration. Logs what would have been sent instead of
/// sending it.
/// </summary>
internal sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogWarning(
            "No SMTP sender is configured (Smtp:Host is empty), so this email was NOT sent. " +
            "To: {ToEmail} <{ToName}>, Subject: {Subject}",
            message.ToEmail,
            message.ToName,
            message.Subject);

        return Task.CompletedTask;
    }
}

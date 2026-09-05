namespace Application.Abstractions.Email;

/// <summary>
/// Transport strategy for outgoing email. Concrete implementations live in Infrastructure and are
/// chosen at startup based on configuration (e.g. SMTP via MailKit, or a logging fallback when no
/// SMTP server is configured) — see Infrastructure/DependencyInjection.cs's AddEmail.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public sealed record EmailMessage(string ToEmail, string ToName, string Subject, string HtmlBody);

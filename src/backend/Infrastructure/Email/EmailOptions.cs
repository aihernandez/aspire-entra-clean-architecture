namespace Infrastructure.Email;

/// <summary>
/// SMTP transport settings, bound from the "Smtp" config section. In Development, Host/Port are
/// overridden at startup from the Aspire "mailpit" connection string instead of this section —
/// see DependencyInjection.AddEmail.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public bool UseSsl { get; set; }

    public string FromEmail { get; set; } = "no-reply@example.com";

    public string FromName { get; set; } = "Aspire Identity Template";
}

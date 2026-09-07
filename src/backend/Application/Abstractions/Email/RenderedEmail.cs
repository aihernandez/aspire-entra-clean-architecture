namespace Application.Abstractions.Email;

/// <summary>
/// The recipient-independent result of rendering one email template. Keeping the subject and body
/// together makes the template the single owner of all content sent for that email kind.
/// </summary>
public sealed record RenderedEmail(string Subject, string HtmlBody);

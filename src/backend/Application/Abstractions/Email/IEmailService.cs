namespace Application.Abstractions.Email;

/// <summary>
/// Combines an <see cref="IEmailTemplate{TModel}"/> (content) with the registered
/// <see cref="IEmailSender"/> (transport) to render and send one email. This is the entry point
/// Application handlers use — they never talk to <see cref="IEmailSender"/> directly.
/// </summary>
public interface IEmailService
{
    Task SendAsync<TModel>(
        string toEmail,
        string toName,
        IEmailTemplate<TModel> template,
        TModel model,
        CancellationToken cancellationToken = default);
}

namespace Application.Abstractions.Email;

/// <summary>
/// Renders and sends the one template registered for <typeparamref name="TModel"/>. Application
/// handlers supply only the recipient and model; template selection, rendering, subject, and
/// transport remain encapsulated behind this boundary.
/// </summary>
public interface IEmailService<in TModel> where TModel : notnull
{
    Task SendAsync(
        string toEmail,
        string toName,
        TModel model,
        CancellationToken cancellationToken = default);
}

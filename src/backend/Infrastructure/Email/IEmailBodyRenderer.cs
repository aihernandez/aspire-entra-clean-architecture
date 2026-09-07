namespace Infrastructure.Email;

/// <summary>
/// Renders the HTML body for the single Razor component registered for <typeparamref name="TModel"/>.
/// Keeping this boundary closed over the model type makes email templates independently unit-testable.
/// </summary>
internal interface IEmailBodyRenderer<in TModel> where TModel : notnull
{
    Task<string> RenderAsync(TModel model, CancellationToken cancellationToken = default);
}

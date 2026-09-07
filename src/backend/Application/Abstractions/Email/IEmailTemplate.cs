namespace Application.Abstractions.Email;

/// <summary>
/// Content strategy for a single kind of email (e.g. "welcome", "confirm email"). Each email kind
/// is its own small class implementing this interface for its model type, resolved by the .NET DI
/// container's generic-type resolution — no factory or switch statement needed. Each template
/// owns both its subject and HTML body; concrete implementations render Razor components in
/// Infrastructure.
/// </summary>
public interface IEmailTemplate<in TModel> where TModel : notnull
{
    Task<RenderedEmail> RenderAsync(TModel model, CancellationToken cancellationToken = default);
}

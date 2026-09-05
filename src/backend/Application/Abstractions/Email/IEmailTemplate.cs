namespace Application.Abstractions.Email;

/// <summary>
/// Content strategy for a single kind of email (e.g. "welcome", "confirm email"). Each email kind
/// is its own small class implementing this interface for its model type, resolved by the .NET DI
/// container's generic-type resolution — no factory or switch statement needed. Concrete
/// implementations live in Infrastructure, where they render shared HTML templates.
/// </summary>
public interface IEmailTemplate<in TModel>
{
    string Subject { get; }

    Task<string> RenderAsync(TModel model, CancellationToken cancellationToken = default);
}

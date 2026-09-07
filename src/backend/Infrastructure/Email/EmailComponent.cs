using Microsoft.AspNetCore.Components;

namespace Infrastructure.Email;

/// <summary>
/// Strongly typed root for an email Razor component. The generic constraint on the renderer ties
/// the component and its model together at compile time.
/// </summary>
public abstract class EmailComponent<TModel> : ComponentBase where TModel : notnull
{
    [Parameter]
    [EditorRequired]
    public TModel Model { get; set; } = default!;
}

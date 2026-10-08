namespace Infrastructure.Email;

/// <summary>Renders a compiled, strongly typed Razor view to an email body.</summary>
internal interface IRazorEmailRenderer
{
    Task<string> RenderAsync<TModel>(
        string viewPath,
        TModel model,
        CancellationToken cancellationToken = default)
        where TModel : notnull;
}

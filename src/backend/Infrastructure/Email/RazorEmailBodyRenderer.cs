using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Email;

/// <summary>
/// Renders a precompiled Razor component to static HTML without an HTTP request or MVC context.
/// The component mapping is established once in DI; consumers pass only the strongly typed model.
/// </summary>
internal sealed class RazorEmailBodyRenderer<TComponent, TModel>(
    IServiceProvider serviceProvider,
    ILoggerFactory loggerFactory) : IEmailBodyRenderer<TModel>
    where TComponent : EmailComponent<TModel>
    where TModel : notnull
{
    public async Task<string> RenderAsync(
        TModel model,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var renderer = new HtmlRenderer(serviceProvider, loggerFactory);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    [nameof(EmailComponent<TModel>.Model)] = model
                });

            HtmlRootComponent output = await renderer.RenderComponentAsync<TComponent>(
                parameters);

            return output.ToHtmlString();
        });
    }
}

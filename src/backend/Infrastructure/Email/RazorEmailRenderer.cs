using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

namespace Infrastructure.Email;

/// <summary>Executes precompiled MVC views without an incoming HTTP request.</summary>
internal sealed class RazorEmailRenderer(
    IRazorViewEngine viewEngine,
    ITempDataProvider tempDataProvider,
    IModelMetadataProvider metadataProvider,
    IServiceProvider services) : IRazorEmailRenderer
{
    public async Task<string> RenderAsync<TModel>(
        string viewPath,
        TModel model,
        CancellationToken cancellationToken = default)
        where TModel : notnull
    {
        cancellationToken.ThrowIfCancellationRequested();

        var actionContext = new ActionContext(
            new DefaultHttpContext { RequestServices = services },
            new RouteData(),
            new ActionDescriptor());

        ViewEngineResult viewResult = viewEngine.GetView(
            executingFilePath: null,
            viewPath,
            isMainPage: true);

        if (!viewResult.Success || viewResult.View is null)
        {
            throw new InvalidOperationException($"Email view not found: {viewPath}.");
        }

        var viewData = new ViewDataDictionary<TModel>(metadataProvider, new ModelStateDictionary())
        {
            Model = model
        };

        using var writer = new StringWriter();
        var viewContext = new ViewContext(
            actionContext,
            viewResult.View,
            viewData,
            new TempDataDictionary(actionContext.HttpContext, tempDataProvider),
            writer,
            new HtmlHelperOptions());

        await viewResult.View.RenderAsync(viewContext);
        cancellationToken.ThrowIfCancellationRequested();

        return writer.ToString();
    }
}

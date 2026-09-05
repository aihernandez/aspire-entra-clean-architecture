using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Options;
using Scriban;

namespace Infrastructure.Email;

/// <summary>
/// Renders an email in two passes: the content template (e.g. "ConfirmEmail") first, then that
/// HTML is dropped into the shared "_Layout" template's {{ content }} slot along with the common
/// branding variables. Every email — however many kinds get added later — shares one layout and
/// one rendering code path; only the small content template differs per kind.
/// </summary>
internal sealed class EmailTemplateEngine(IOptions<EmailBrandingOptions> branding)
{
    private static readonly ConcurrentDictionary<string, Template> ParsedTemplates = new();

    public string Render<TModel>(string templateName, TModel model)
    {
        string content = RenderResource(templateName, model);

        EmailBrandingOptions brandingOptions = branding.Value;

        var layoutModel = new
        {
            Content = content,
            brandingOptions.AppName,
            brandingOptions.SupportEmail,
            brandingOptions.PrimaryColor,
            DateTime.UtcNow.Year
        };

        return RenderResource("_Layout", layoutModel);
    }

    private static string RenderResource<TModel>(string templateName, TModel model)
    {
        Template template = ParsedTemplates.GetOrAdd(templateName, LoadTemplate);

        // Keep C# property names as-is (PascalCase) instead of Scriban's default snake_case
        // renaming, so templates read "{{ FirstName }}" — an exact match to the model.
        return template.Render(model, member => member.Name);
    }

    private static Template LoadTemplate(string templateName)
    {
        string resourceName = $"Infrastructure.Email.Templates.{templateName}.html";
        Assembly assembly = typeof(EmailTemplateEngine).Assembly;

        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded email template '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);

        string text = reader.ReadToEnd();
        var parsed = Template.Parse(text, resourceName);

        if (parsed.HasErrors)
        {
            throw new InvalidOperationException(
                $"Email template '{resourceName}' failed to parse: {string.Join(", ", parsed.Messages)}");
        }

        return parsed;
    }
}

namespace Infrastructure.Email;

/// <summary>
/// The "common elements" every email shares — bound from "Email:Branding". Change these once and
/// every email template picks up the new name/color, since they all render inside the same
/// _Layout.html.
/// </summary>
public sealed class EmailBrandingOptions
{
    public const string SectionName = "Email:Branding";

    public string AppName { get; set; } = "Aspire Identity Template";

    public string SupportEmail { get; set; } = "support@example.com";

    public string PrimaryColor { get; set; } = "#4f46e5";
}

using Microsoft.AspNetCore.Authentication;

namespace Infrastructure.Authentication;

/// <summary>
/// The stand-in identity used when the template runs without an Azure tenant. See
/// EntraIdMigration.md, D8 for why this exists and the constraints it runs under.
/// </summary>
internal sealed class DevelopmentAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SectionName = "DevelopmentAuthentication";

    public Guid ObjectId { get; set; } = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public Guid TenantId { get; set; } = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public string Email { get; set; } = "dev.user@localhost";

    public string FirstName { get; set; } = "Dev";

    public string LastName { get; set; } = "User";

    /// <summary>
    /// App Roles the stand-in user carries. When left empty the handler grants the full set, so a
    /// freshly cloned template can reach every screen; narrow it locally to exercise authorization
    /// failures.
    /// </summary>
    public IList<string> Roles { get; } = [];
}

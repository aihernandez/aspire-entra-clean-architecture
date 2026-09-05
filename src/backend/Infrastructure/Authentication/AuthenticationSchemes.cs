namespace Infrastructure.Authentication;

internal static class AuthenticationSchemes
{
    /// <summary>
    /// Access tokens issued by the Microsoft identity platform, validated by
    /// <c>Microsoft.Identity.Web</c>. Named "Bearer" because that is the scheme name
    /// <c>AddMicrosoftIdentityWebApi</c> defaults to and what the OpenAPI document advertises.
    /// </summary>
    public const string EntraId = "Bearer";

    /// <summary>
    /// Development-only stand-in so the template boots without an Azure tenant. Registered
    /// exclusively under <c>IHostEnvironment.IsDevelopment()</c> — see EntraIdMigration.md, D8.
    /// </summary>
    public const string Development = "Development";
}

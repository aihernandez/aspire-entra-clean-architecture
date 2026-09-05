using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using SharedKernel;

namespace Infrastructure.Authentication;

/// <summary>
/// Authenticates every request as a fixed, configured person, so that <c>dotnet run</c> works on a
/// machine with no Azure tenant, no app registration and nobody available to grant admin consent.
/// It produces the same shape of principal Entra ID would — an <c>oid</c>, a <c>tid</c>, a distinct
/// <c>sub</c> and a <c>roles</c> claim — so nothing downstream needs a development branch.
/// <para>
/// The <c>X-Dev-*</c> headers let a caller vary that identity per request: impersonate a second
/// person, drop to a non-administrator role, or go anonymous. That is what makes the integration
/// suite possible without real tokens, and it doubles as a way to check authorization failures by
/// hand in local development.
/// </para>
/// <para>
/// This is an authentication bypass. It is registered only when the host environment is
/// Development, and <c>AddAuthenticationInternal</c> refuses to start the application if its
/// configuration section appears in any other environment.
/// </para>
/// </summary>
internal sealed class DevelopmentAuthenticationHandler(
    IOptionsMonitor<DevelopmentAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<DevelopmentAuthenticationOptions>(options, logger, encoder)
{
    internal const string AnonymousHeader = "X-Dev-Anonymous";
    internal const string ObjectIdHeader = "X-Dev-ObjectId";
    internal const string RolesHeader = "X-Dev-Roles";
    internal const string EmailHeader = "X-Dev-Email";

    /// <summary>
    /// Explicit "no app roles at all". An empty header value does not reliably survive the round
    /// trip, so the absence of roles needs a value of its own.
    /// </summary>
    internal const string NoRolesValue = "none";

    private const string RolesClaimType = "roles";
    private const string UsernameClaimType = "preferred_username";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.ContainsKey(AnonymousHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        DevelopmentAuthenticationOptions current = Options;

        Guid objectId =
            Guid.TryParse(Request.Headers[ObjectIdHeader].ToString(), out Guid requested)
                ? requested
                : current.ObjectId;

        string email = Request.Headers[EmailHeader].ToString() is { Length: > 0 } requestedEmail
            ? requestedEmail
            : current.Email;

        var claims = new List<Claim>
        {
            new(ClaimConstants.Oid, objectId.ToString()),
            new(ClaimConstants.Tid, current.TenantId.ToString()),

            // Deliberately different from the oid. A token whose sub equals its oid is an app-only
            // token, and the provisioning middleware refuses to give those a user profile.
            new(ClaimConstants.Sub, $"dev-{objectId}"),

            new(UsernameClaimType, email),
            new("given_name", current.FirstName),
            new("family_name", current.LastName)
        };

        claims.AddRange(ResolveRoles(current).Select(role => new Claim(RolesClaimType, role)));

        var identity = new ClaimsIdentity(claims, Scheme.Name, UsernameClaimType, RolesClaimType);
        var principal = new ClaimsPrincipal(identity);

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }

    private IEnumerable<string> ResolveRoles(DevelopmentAuthenticationOptions current)
    {
        if (Request.Headers.TryGetValue(RolesHeader, out Microsoft.Extensions.Primitives.StringValues requested))
        {
            string value = requested.ToString();

            // "none" means assigned to the tenant but not to this application — the state D6 exists
            // to refuse, and one worth being able to reproduce on demand.
            if (string.Equals(value, NoRolesValue, StringComparison.OrdinalIgnoreCase))
            {
                return [];
            }

            return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return current.Roles.Count > 0 ? current.Roles : [RoleNames.Admin, RoleNames.Member];
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Infrastructure.Authorization;

internal sealed class PermissionAuthorizationPolicyProvider : DefaultAuthorizationPolicyProvider
{
    private readonly AuthorizationOptions _authorizationOptions;
    private readonly string[] _schemes;

    public PermissionAuthorizationPolicyProvider(
        IOptions<AuthorizationOptions> options,
        IAuthenticationSchemeRegistry schemeRegistry)
        : base(options)
    {
        _authorizationOptions = options.Value;
        _schemes = schemeRegistry.Schemes;
    }

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        AuthorizationPolicy? policy = await base.GetPolicyAsync(policyName);

        if (policy is not null)
        {
            return policy;
        }

        // The accepted schemes have to be named explicitly. A policy built without them falls back
        // to the default scheme only, so as soon as a second scheme exists — Entra ID in
        // production, the development stand-in locally — tokens from the other one authenticate
        // correctly and then fail every permission check with a 403 that explains nothing.
        AuthorizationPolicy permissionPolicy = new AuthorizationPolicyBuilder(_schemes)
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(policyName))
            .Build();

        _authorizationOptions.AddPolicy(policyName, permissionPolicy);

        return permissionPolicy;
    }
}

/// <summary>
/// The authentication schemes registered for this host, so authorization policies can accept every
/// one of them. Which schemes exist is decided at startup, from configuration and environment.
/// </summary>
internal interface IAuthenticationSchemeRegistry
{
    string[] Schemes { get; }
}

internal sealed class AuthenticationSchemeRegistry(string[] schemes) : IAuthenticationSchemeRegistry
{
    public string[] Schemes { get; } = schemes;
}

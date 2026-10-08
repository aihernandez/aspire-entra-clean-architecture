using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Web.Api;

namespace IntegrationTests;

// Exercises the real HTTP permission/provisioning pipeline with synthetic principals.
// Token signature, issuer and audience validation still require the separate Entra check.
public sealed class PrincipalAuthorizationTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Theory]
    [InlineData("app-idtyp")]
    [InlineData("app-mapped-sub")]
    [InlineData("missing-oid")]
    [InlineData("missing-tid")]
    [InlineData("malformed-oid")]
    [InlineData("malformed-tid")]
    [InlineData("missing-scope")]
    [InlineData("wrong-scope")]
    public async Task InvalidPrincipal_Should_ReturnForbidden_WithoutProvisioning(string scenario)
    {
        List<Claim> claims = CreateClaims();
        switch (scenario)
        {
            case "app-idtyp": claims.Add(new Claim("idtyp", "app")); break;
            case "app-mapped-sub": claims.Add(new Claim(ClaimTypes.NameIdentifier, claims[0].Value)); break;
            case "missing-oid": claims.RemoveAll(claim => claim.Type == "oid"); break;
            case "missing-tid": claims.RemoveAll(claim => claim.Type == "tid"); break;
            case "malformed-oid": claims[0] = new Claim("oid", "invalid"); break;
            case "malformed-tid": claims[1] = new Claim("tid", "invalid"); break;
            case "missing-scope": claims.RemoveAll(claim => claim.Type == "scp"); break;
            case "wrong-scope": claims[2] = new Claim("scp", "access_as_user_extra"); break;
        }

        using WebApplicationFactory<Program> app = CreateApp(claims);
        using HttpClient client = app.CreateClient();
        int before = Factory.SqlCommands.Count;

        (await client.GetAsync("users/me")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.GetAsync("users")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        Factory.SqlCommands.Count.ShouldBe(before);
    }

    [Fact]
    public async Task DelegatedAdmin_Should_ReachProtectedEndpoints_AndProvisionUser()
    {
        using WebApplicationFactory<Program> app = CreateApp(CreateClaims());
        using HttpClient client = app.CreateClient();
        int before = Factory.SqlCommands.Count;

        (await client.GetAsync("users/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("users")).StatusCode.ShouldBe(HttpStatusCode.OK);
        Factory.SqlCommands.Count.ShouldBeGreaterThan(before);
    }

    private static List<Claim> CreateClaims() =>
    [
        new("oid", Guid.NewGuid().ToString()),
        new("tid", "22222222-2222-2222-2222-222222222222"),
        new("scp", "openid access_as_user"),
        new("roles", "Admin"),
        new("preferred_username", $"synthetic-{Guid.NewGuid():N}@example.com"),
        new("name", "Synthetic User")
    ];

    private WebApplicationFactory<Program> CreateApp(List<Claim> claims) =>
        Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            // Permission policies explicitly name the registered Development scheme. Replace its
            // handler in this test host instead of adding a second scheme that policies would ignore.
            services.AddOptions<SyntheticOptions>("Development")
                .Configure(options => options.Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")));
            services.AddTransient<SyntheticHandler>();
            services.PostConfigure<AuthenticationOptions>(options =>
                options.SchemeMap["Development"].HandlerType = typeof(SyntheticHandler));
        }));

    private sealed class SyntheticOptions : AuthenticationSchemeOptions
    {
        public ClaimsPrincipal Principal { get; set; } = new();
    }

    private sealed class SyntheticHandler(
        IOptionsMonitor<SyntheticOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<SyntheticOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(Options.Principal, Scheme.Name)));
    }
}

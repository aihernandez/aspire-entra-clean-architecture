using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

/// <summary>
/// Enforces D6 of EntraIdMigration.md: no endpoint may authorize on authentication alone.
/// <para>
/// The trap this guards against is subtle. Checking only that the token comes from our tenant and
/// carries an <c>oid</c> reads like "anyone from our company", but it also admits every service
/// principal registered in that tenant — every automation and script anybody ever created. Demanding
/// a permission, and therefore an App Role, is what makes the intended sentence true.
/// </para>
/// <para>
/// This runs as an integration test rather than a NetArchTest rule because the authorization
/// metadata is attached by a fluent call inside a lambda; only the built endpoint graph knows the
/// truth about it.
/// </para>
/// </summary>
public sealed class EndpointAuthorizationTests(IntegrationTestWebAppFactory factory)
    : BaseIntegrationTest(factory)
{
    /// <summary>
    /// Infrastructure endpoints that are deliberately open: liveness and readiness probes, and the
    /// OpenAPI document (served in Development only).
    /// </summary>
    private static readonly string[] UnauthenticatedByDesign =
    [
        "health",
        "alive",
        "openapi",

        // Scalar's API-reference UI and its static assets. Mapped only in Development, and
        // documentation nobody can read is not a security control.
        "scalar",

        // The sign-in bootstrap. It has to be reachable before anyone can hold a token — that is
        // its whole purpose — and it returns only public identifiers (client id, tenant, scope)
        // that appear in the browser's address bar during any sign-in redirect anyway.
        "auth-config"
    ];

    [Fact]
    public void EveryApplicationEndpoint_Should_RequireAPermission()
    {
        // Arrange
        EndpointDataSource endpointDataSource = Factory.Services.GetRequiredService<EndpointDataSource>();

        // Act
        List<string> unprotected = [];

        foreach (Endpoint endpoint in endpointDataSource.Endpoints)
        {
            if (endpoint is not RouteEndpoint routeEndpoint)
            {
                continue;
            }

            string pattern = routeEndpoint.RoutePattern.RawText ?? string.Empty;

            if (UnauthenticatedByDesign.Any(open =>
                    pattern.TrimStart('/').StartsWith(open, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            IReadOnlyList<IAuthorizeData> authorizeData = [.. endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()];

            // A bare [Authorize] / RequireAuthorization() has no policy: it means "any authenticated
            // caller", which is exactly what must not exist here.
            bool hasPermission = authorizeData.Any(data => !string.IsNullOrWhiteSpace(data.Policy));

            if (!hasPermission)
            {
                unprotected.Add($"{string.Join(',', routeEndpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["?"])} /{pattern.TrimStart('/')}");
            }
        }

        // Assert
        unprotected.ShouldBeEmpty(
            "These endpoints authorize on authentication alone. Add .HasPermission(...) so they " +
            "require an App Role: " + string.Join("; ", unprotected));
    }
}

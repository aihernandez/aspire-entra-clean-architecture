using Infrastructure.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Web.Api;

namespace IntegrationTests;

public sealed class IntegrationTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _dbContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // The suite runs against the development authentication scheme: acquiring real Entra ID
        // tokens would need a live tenant, an interactive sign-in and a secret in CI. Tests supply
        // claims through X-Dev-* headers instead — see BaseIntegrationTest.
        builder.UseEnvironment("Development");

        builder.UseSetting("ConnectionStrings:Database", _dbContainer.GetConnectionString());

        // Force the stand-in scheme even if a developer has AzureAd configured locally, so the
        // suite behaves identically on every machine.
        builder.UseSetting("AzureAd:ClientId", string.Empty);
        builder.UseSetting("DevelopmentAuthentication:TenantId", "22222222-2222-2222-2222-222222222222");

        // Relax rate limiting so the test suite is not throttled.
        builder.UseSetting("RateLimiting:Global:PermitLimit", "100000");
        builder.UseSetting("RateLimiting:Authentication:PermitLimit", "100000");
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}

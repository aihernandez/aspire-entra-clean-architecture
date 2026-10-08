using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Web.Api;

namespace IntegrationTests;

public sealed class AuthAndHealthTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record AuthConfigDto(bool Enabled);

    [Fact]
    public async Task DevelopmentScheme_Should_AdvertiseTheLocalBypass()
    {
        using HttpClient client = CreateMemberClient(out _);
        int commandsBefore = Factory.SqlCommands.Count;
        AuthConfigDto? config = await client.GetFromJsonAsync<AuthConfigDto>("auth-config");

        config!.Enabled.ShouldBeFalse();
        (await client.GetAsync("alive")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("ready")).StatusCode.ShouldBe(HttpStatusCode.OK);
        Factory.SqlCommands.Count.ShouldBe(commandsBefore);
    }

    [Fact]
    public async Task DevelopmentProbes_Should_SurviveDatabaseOutage_WithoutProvisioning()
    {
        using HttpClient client = CreateMemberClient(out _);
        (await client.GetAsync("ready")).StatusCode.ShouldBe(HttpStatusCode.OK);
        int commandsBefore = Factory.SqlCommands.Count;
        await Factory.PauseDatabaseAsync();
        try
        {
            (await client.GetAsync("alive")).StatusCode.ShouldBe(HttpStatusCode.OK);
            (await client.GetAsync("auth-config")).StatusCode.ShouldBe(HttpStatusCode.OK);
            (await client.GetAsync("ready").WaitAsync(TimeSpan.FromSeconds(20)))
                .StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            Factory.SqlCommands.Count.ShouldBe(commandsBefore);
        }
        finally
        {
            await Factory.UnpauseDatabaseAsync();
        }

        (await client.GetAsync("ready")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task IncompleteStagingConfiguration_Should_FailClosed()
    {
        using WebApplicationFactory<Program> root = new();
        using WebApplicationFactory<Program> app = root.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Staging");
            builder.UseSetting("ConnectionStrings:Database",
                "Server=127.0.0.1,1;Database=unused;User Id=unused;Password=unused;TrustServerCertificate=true;Connect Timeout=1");
            builder.UseSetting("AzureAd:TenantId", string.Empty);
            builder.UseSetting("AzureAd:ClientId", string.Empty);
            builder.UseSetting("AzureAd:SpaClientId", string.Empty);
        });
        using HttpClient client = app.CreateClient();

        (await client.GetAsync("auth-config")).StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await client.GetAsync("alive")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("ready")).StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await client.GetAsync("users/me")).StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }
}

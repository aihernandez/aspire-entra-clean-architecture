using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Web.Api;

namespace IntegrationTests;

public sealed class RateLimitingTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task ExhaustedQuota_Should_RejectBeforeSql_AndKeepProbesAvailable()
    {
        using WebApplicationFactory<Program> app = Factory.WithWebHostBuilder(builder =>
            builder.UseSetting("RateLimiting:Global:PermitLimit", "2"));
        using HttpClient client = app.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-ObjectId", Guid.NewGuid().ToString());
        // Public bootstrap consumes the same quota, but must not create a local profile.
        (await client.GetAsync("auth-config")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("auth-config")).StatusCode.ShouldBe(HttpStatusCode.OK);
        int commandsBefore = Factory.SqlCommands.Count;

        HttpResponseMessage rejected = await client.GetAsync("users/me");

        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        rejected.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        ProblemDto? problem = await rejected.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Status.ShouldBe(429);
        (await client.GetAsync("alive")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("ready")).StatusCode.ShouldBe(HttpStatusCode.OK);
        Factory.SqlCommands.Count.ShouldBe(commandsBefore);
    }

    [Fact]
    public async Task UsersWithSameDisplayIdentity_Should_HaveIndependentQuotas()
    {
        using WebApplicationFactory<Program> app = Factory.WithWebHostBuilder(builder =>
            builder.UseSetting("RateLimiting:Global:PermitLimit", "1"));
        using HttpClient first = app.CreateClient();
        using HttpClient second = app.CreateClient();
        first.DefaultRequestHeaders.Add("X-Dev-ObjectId", Guid.NewGuid().ToString());
        second.DefaultRequestHeaders.Add("X-Dev-ObjectId", Guid.NewGuid().ToString());
        // Both retain the default email/name. Only their immutable object ids differ.

        (await first.GetAsync("users/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await first.GetAsync("users/me")).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await second.GetAsync("users/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private sealed record ProblemDto(int Status);
}

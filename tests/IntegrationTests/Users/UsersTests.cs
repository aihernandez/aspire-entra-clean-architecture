using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Users;

public sealed class UsersTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record UserDto(Guid Id, string Email, string FirstName, string LastName, bool IsActive);

    private sealed record PagedUsers(List<UserDto> Items, int TotalCount);

    [Fact]
    public async Task Request_Should_ProvisionLocalUser_OnFirstAuthenticatedCall()
    {
        // Arrange — an oid that has never been seen before. There is no registration endpoint to
        // call: the first authenticated request is what brings the local record into existence.
        using HttpClient client = CreateMemberClient(out Guid objectId);

        // Act — /users/me takes no parameters, so it is the cheapest authenticated call there is.
        HttpResponseMessage response = await client.GetAsync("users/me");

        // Assert
        response.EnsureSuccessStatusCode();

        using HttpClient admin = CreateAdminClient();
        HttpResponseMessage listResponse = await admin.GetAsync("users?pageNumber=1&pageSize=100");
        listResponse.EnsureSuccessStatusCode();

        PagedUsers? users = await listResponse.Content.ReadFromJsonAsync<PagedUsers>();
        users!.Items.ShouldContain(user => user.Email == $"user-{objectId:N}@example.com");
    }

    [Fact]
    public async Task Provisioning_Should_BeIdempotent_AcrossRequests()
    {
        // Arrange
        var objectId = Guid.NewGuid();
        using HttpClient client = CreateClientAs(objectId, MemberRole);

        // Act — the same oid three times must not create three people.
        await client.GetAsync("users/me");
        await client.GetAsync("users/me");
        await client.GetAsync("users/me");

        // Assert
        using HttpClient admin = CreateAdminClient();
        PagedUsers? users = await admin.GetFromJsonAsync<PagedUsers>("users?pageNumber=1&pageSize=100");

        users!.Items.Count(user => user.Email == $"user-{objectId:N}@example.com").ShouldBe(1);
    }

    [Fact]
    public async Task GetAllUsers_Should_ReturnForbidden_ForNonAdministrator()
    {
        // Arrange
        using HttpClient client = CreateMemberClient(out _);

        // Act
        HttpResponseMessage response = await client.GetAsync("users");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAllUsers_Should_Succeed_ForAdministrator()
    {
        // Arrange
        using HttpClient client = CreateAdminClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("users");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Endpoints_Should_ReturnForbidden_WhenTokenCarriesNoAppRole()
    {
        // A person who exists in the tenant but was never assigned to this application. Validating
        // the tenant and the presence of an oid is NOT the same as authorizing them — this is the
        // case that would also let every service principal in the tenant through.
        using HttpClient client = CreateUnassignedClient();

        HttpResponseMessage me = await client.GetAsync("users/me");
        HttpResponseMessage users = await client.GetAsync("users");

        me.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        users.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Endpoints_Should_ReturnUnauthorized_WhenUnauthenticated()
    {
        // Arrange
        using HttpClient client = CreateAnonymousClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("users");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUserById_Should_ReturnForbidden_WhenReadingSomebodyElseAsNonAdministrator()
    {
        // Arrange — provision two distinct people.
        using HttpClient other = CreateMemberClient(out Guid otherObjectId);
        await other.GetAsync("users/me");

        using HttpClient admin = CreateAdminClient();
        PagedUsers? users = await admin.GetFromJsonAsync<PagedUsers>("users?pageNumber=1&pageSize=100");
        UserDto target = users!.Items.Single(user => user.Email == $"user-{otherObjectId:N}@example.com");

        using HttpClient caller = CreateMemberClient(out _);
        await caller.GetAsync("users/me");

        // Act
        HttpResponseMessage response = await caller.GetAsync($"users/{target.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}

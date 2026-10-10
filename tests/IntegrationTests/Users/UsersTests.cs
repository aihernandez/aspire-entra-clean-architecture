using System.Net;
using System.Net.Http.Json;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Users;

public sealed class UsersTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record UserDto(Guid Id, string Email, string FirstName, string LastName, bool IsActive);
    private sealed record CurrentUserDto(Guid Id, string[] Roles, string[] Permissions);

    private sealed record PagedUsers(List<UserDto> Items, int TotalCount);

    private sealed record ProblemDto(int Status, string Title);

    [Theory]
    [InlineData("pageNumber=2147483647&pageSize=20")]
    [InlineData("pageNumber=0&pageSize=20")]
    [InlineData("pageNumber=1&pageSize=101")]
    public async Task GetAllUsers_Should_ReturnBadRequest_ForInvalidPagination(string query)
    {
        using HttpClient client = CreateAdminClient();

        HttpResponseMessage response = await client.GetAsync($"users?{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Status.ShouldBe(400);
        problem.Title.ShouldBe("Users.InvalidPagination");
    }

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
    public async Task ConcurrentFirstRequests_Should_ResolveToOneLocalUser()
    {
        var objectId = Guid.NewGuid();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<CurrentUserDto?> RequestAsync()
        {
            using HttpClient client = CreateClientAs(objectId, MemberRole);
            await gate.Task;
            return await client.GetFromJsonAsync<CurrentUserDto>("users/me");
        }

        Task<CurrentUserDto?>[] requests = Enumerable.Range(0, 8)
            .Select(_ => RequestAsync())
            .ToArray();
        gate.SetResult();
        CurrentUserDto?[] results = await Task.WhenAll(requests);

        results.ShouldAllBe(user => user != null && user.Id == results[0]!.Id);
        using HttpClient admin = CreateAdminClient();
        PagedUsers? users = await admin.GetFromJsonAsync<PagedUsers>("users?pageNumber=1&pageSize=100");
        users!.Items.Count(user => user.Email == $"user-{objectId:N}@example.com").ShouldBe(1);
    }

    [Fact]
    public async Task GetCurrent_Should_ReturnEffectiveRolesAndPermissions()
    {
        using HttpClient member = CreateMemberClient(out _);
        CurrentUserDto? memberProfile = await member.GetFromJsonAsync<CurrentUserDto>("users/me");
        memberProfile!.Roles.ShouldContain(MemberRole);
        memberProfile.Permissions.ShouldNotContain("users:read-all");

        using HttpClient admin = CreateAdminClient();
        CurrentUserDto? adminProfile = await admin.GetFromJsonAsync<CurrentUserDto>("users/me");
        adminProfile!.Permissions.ShouldContain("users:read-all");
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
    public async Task Administrator_Should_DeactivateAndReactivateUser_WhileKeepingTheRow()
    {
        using HttpClient member = CreateMemberClient(out _);
        CurrentUserDto? target = await member.GetFromJsonAsync<CurrentUserDto>("users/me");
        using HttpClient admin = CreateAdminClient();

        using HttpClient anotherMember = CreateMemberClient(out _);
        HttpResponseMessage forbidden = await anotherMember.DeleteAsync($"users/{target!.Id}");
        forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        HttpResponseMessage deleted = await admin.DeleteAsync($"users/{target.Id}");
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await member.GetAsync("users/me")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        PagedUsers? visible = await admin.GetFromJsonAsync<PagedUsers>(
            "users?pageNumber=1&pageSize=100&includeInactive=true");
        visible!.Items.Single(user => user.Id == target.Id).IsActive.ShouldBeFalse();

        HttpResponseMessage restored = await admin.PutAsJsonAsync(
            $"users/{target.Id}/status", new { isActive = true });
        restored.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await member.GetAsync("users/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Administrator_Should_NotDeactivateOwnAccount()
    {
        using HttpClient admin = CreateAdminClient();
        CurrentUserDto? self = await admin.GetFromJsonAsync<CurrentUserDto>("users/me");

        HttpResponseMessage response = await admin.DeleteAsync($"users/{self!.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Endpoints_Should_ReturnForbidden_WhenTokenCarriesNoAppRole()
    {
        // A person who exists in the tenant but was never assigned to this application. Validating
        // the tenant and the presence of an oid is NOT the same as authorizing them — this is the
        // case that would also let every service principal in the tenant through.
        using HttpClient client = CreateUnassignedClient();
        int commandsBefore = Factory.SqlCommands.Count;

        HttpResponseMessage me = await client.GetAsync("users/me");
        HttpResponseMessage users = await client.GetAsync("users");

        me.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        users.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        Factory.SqlCommands.Count.ShouldBe(commandsBefore);
    }

    [Fact]
    public async Task DeactivatedUser_Should_RemainForbidden_AfterPermissionAuthorization()
    {
        using HttpClient client = CreateMemberClient(out Guid objectId);
        (await client.GetAsync("users/me")).EnsureSuccessStatusCode();
        await using (AsyncServiceScope scope = Factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Domain.Users.User user = await database.Users.SingleAsync(person => person.EntraObjectId == objectId);
            user.IsActive = false;
            await database.SaveChangesAsync();
        }

        HttpResponseMessage response = await client.GetAsync("users/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).ShouldContain("deactivated");
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

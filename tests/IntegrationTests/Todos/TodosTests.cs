using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Todos;

public sealed class TodosTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private sealed record TodoDto(Guid Id, Guid UserId, string Description, bool IsCompleted);

    private sealed record MeDto(Guid Id, string Email);

    /// <summary>
    /// Resolves the caller's <b>local</b> user id. The token only carries the directory's oid, so
    /// this is the round-trip any real client also has to make.
    /// </summary>
    private static async Task<Guid> GetLocalUserIdAsync(HttpClient client)
    {
        MeDto? me = await client.GetFromJsonAsync<MeDto>("users/me");

        return me!.Id;
    }

    [Fact]
    public async Task GetTodo_Should_ReturnUnauthorized_WhenUnauthenticated()
    {
        // Arrange
        using HttpClient client = CreateAnonymousClient();

        // Act
        HttpResponseMessage response = await client.GetAsync($"todos/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTodo_Should_ReturnForbidden_WhenCallerHoldsNoAppRole()
    {
        // Arrange
        using HttpClient client = CreateUnassignedClient();

        // Act
        HttpResponseMessage response = await client.GetAsync($"todos/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateTodo_Should_PersistTodo_ThatCanBeRetrievedById()
    {
        // Arrange
        using HttpClient client = CreateMemberClient(out _);
        Guid userId = await GetLocalUserIdAsync(client);

        var createRequest = new
        {
            userId,
            description = "Integration test todo",
            labels = new[] { "integration" },
            priority = 2
        };

        // Act
        HttpResponseMessage createResponse = await client.PostAsJsonAsync("todos", createRequest);

        // Assert
        createResponse.EnsureSuccessStatusCode();
        Guid todoId = await createResponse.Content.ReadFromJsonAsync<Guid>();
        todoId.ShouldNotBe(Guid.Empty);

        HttpResponseMessage getResponse = await client.GetAsync($"todos/{todoId}");
        getResponse.EnsureSuccessStatusCode();

        TodoDto? todo = await getResponse.Content.ReadFromJsonAsync<TodoDto>();
        todo!.Id.ShouldBe(todoId);
        todo.UserId.ShouldBe(userId);
        todo.Description.ShouldBe("Integration test todo");
        todo.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task CompleteTodo_Should_MarkTodoAsCompleted()
    {
        // Arrange
        using HttpClient client = CreateMemberClient(out _);
        Guid userId = await GetLocalUserIdAsync(client);

        var createRequest = new
        {
            userId,
            description = "Todo to complete",
            labels = Array.Empty<string>(),
            priority = 1
        };
        HttpResponseMessage createResponse = await client.PostAsJsonAsync("todos", createRequest);
        createResponse.EnsureSuccessStatusCode();
        Guid todoId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        // Act
        HttpResponseMessage completeResponse = await client.PutAsync($"todos/{todoId}/complete", null);

        // Assert
        completeResponse.EnsureSuccessStatusCode();

        HttpResponseMessage getResponse = await client.GetAsync($"todos/{todoId}");
        getResponse.EnsureSuccessStatusCode();
        TodoDto? todo = await getResponse.Content.ReadFromJsonAsync<TodoDto>();
        todo!.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateTodo_Should_ReturnForbidden_WhenCreatingForSomebodyElse()
    {
        // Arrange
        using HttpClient client = CreateMemberClient(out _);
        await GetLocalUserIdAsync(client);

        var createRequest = new
        {
            userId = Guid.NewGuid(),
            description = "Not mine",
            labels = Array.Empty<string>(),
            priority = 1
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("todos", createRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}

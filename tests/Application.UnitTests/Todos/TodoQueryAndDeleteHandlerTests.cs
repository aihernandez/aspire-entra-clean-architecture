using Application.Abstractions.Authentication;
using Application.Todos;
using Application.Todos.Delete;
using Application.Todos.Get;
using Application.UnitTests.Abstractions;
using Domain.Todos;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.UnitTests.Todos;

public sealed class TodoQueryAndDeleteHandlerTests : BaseHandlerTest
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static TodoItem NewTodo(Guid userId, string description) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Description = description,
        Labels = [],
        CreatedAt = DateTime.UtcNow
    };

    private static IUserContext UserContextFor(Guid userId)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(userId);

        return userContext;
    }

    [Fact]
    public async Task GetTodos_Should_ReturnUnauthorized_WhenAskingForSomebodyElsesList()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new GetTodosQueryHandler(context, UserContextFor(OwnerId));

        // Act
        Result<List<TodoResponse>> result =
            await handler.Handle(new GetTodosQuery(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
    }

    [Fact]
    public async Task GetTodos_Should_ReturnOnlyTheCallersOwnItems()
    {
        // Arrange — a second person's data sitting in the same table is the whole point of the test.
        await using TestDbContext context = CreateDbContext();
        context.TodoItems.AddRange(
            NewTodo(OwnerId, "mine"),
            NewTodo(Guid.NewGuid(), "somebody else's"));
        await context.SaveChangesAsync();

        var handler = new GetTodosQueryHandler(context, UserContextFor(OwnerId));

        // Act
        Result<List<TodoResponse>> result = await handler.Handle(new GetTodosQuery(OwnerId), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldHaveSingleItem().Description.ShouldBe("mine");
    }

    [Fact]
    public async Task DeleteTodo_Should_RemoveTheItemAndRaiseTheDomainEvent()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        TodoItem todo = NewTodo(OwnerId, "to delete");
        context.TodoItems.Add(todo);
        await context.SaveChangesAsync();

        var handler = new DeleteTodoCommandHandler(context, UserContextFor(OwnerId), CreateCache());

        // Act
        Result result = await handler.Handle(new DeleteTodoCommand(todo.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.TodoItems.AnyAsync(t => t.Id == todo.Id)).ShouldBeFalse();
        todo.DomainEvents.ShouldContain(domainEvent => domainEvent is TodoItemDeletedDomainEvent);
    }

    [Fact]
    public async Task DeleteTodo_Should_ReturnNotFound_WhenTheItemBelongsToSomebodyElse()
    {
        // Arrange — ownership is enforced in the same query that finds the row, so another
        // person's item is indistinguishable from a missing one. That is deliberate: a 403 here
        // would confirm the item exists.
        await using TestDbContext context = CreateDbContext();
        TodoItem someoneElses = NewTodo(Guid.NewGuid(), "not yours");
        context.TodoItems.Add(someoneElses);
        await context.SaveChangesAsync();

        var handler = new DeleteTodoCommandHandler(context, UserContextFor(OwnerId), CreateCache());

        // Act
        Result result = await handler.Handle(new DeleteTodoCommand(someoneElses.Id), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TodoItemErrors.NotFound(someoneElses.Id));
        (await context.TodoItems.AnyAsync(t => t.Id == someoneElses.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteTodo_Should_ReturnNotFound_ForAnUnknownId()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var missingId = Guid.NewGuid();

        var handler = new DeleteTodoCommandHandler(context, UserContextFor(OwnerId), CreateCache());

        // Act
        Result result = await handler.Handle(new DeleteTodoCommand(missingId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TodoItemErrors.NotFound(missingId));
    }
}

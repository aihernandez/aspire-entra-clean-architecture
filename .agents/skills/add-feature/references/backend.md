# Backend slice templates

Based on `Application/Todos/Complete/*` and `Web.Api/Endpoints/Todos/Complete.cs`.

## Command + Handler + Validator (state change)

`src/backend/Application/{Entity}/{UseCase}/{UseCase}Command.cs`

```csharp
using Application.Abstractions.Messaging;

namespace Application.Todos.Complete;

public sealed record CompleteTodoCommand(Guid TodoItemId) : ICommand;
```

Use a `record` for simple commands, or a `class` with settable properties when the endpoint needs
to build it from several request fields (see `CreateTodoCommand`). `ICommand` for no return value,
`ICommand<TResponse>` when the handler returns something (e.g. a new id).

`src/backend/Application/{Entity}/{UseCase}/{UseCase}CommandHandler.cs`

```csharp
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Todos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.Todos.Complete;

internal sealed class CompleteTodoCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    IUserContext userContext,
    HybridCache cache)
    : ICommandHandler<CompleteTodoCommand>
{
    public async Task<Result> Handle(CompleteTodoCommand command, CancellationToken cancellationToken)
    {
        TodoItem? todoItem = await context.TodoItems
            .SingleOrDefaultAsync(t => t.Id == command.TodoItemId && t.UserId == userContext.UserId, cancellationToken);

        if (todoItem is null)
        {
            return Result.Failure(TodoItemErrors.NotFound(command.TodoItemId));
        }

        if (todoItem.IsCompleted)
        {
            return Result.Failure(TodoItemErrors.AlreadyCompleted(command.TodoItemId));
        }

        todoItem.IsCompleted = true;
        todoItem.CompletedAt = dateTimeProvider.UtcNow;

        todoItem.Raise(new TodoItemCompletedDomainEvent(todoItem.Id));

        await context.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(TodoCacheKeys.ById(todoItem.UserId, todoItem.Id), cancellationToken);

        return Result.Success();
    }
}
```

Ownership check pattern: filter the query by `userContext.UserId` directly when possible (as
above), or compare `command.UserId != userContext.UserId` and return `UserErrors.Unauthorized()`
(→ `Error.Forbidden`, 403) when the id is a separate field the caller supplies (see
`CreateTodoCommandHandler`). Never use `Error.Failure` for an authorization failure — that maps to
500, not 403.

`src/backend/Application/{Entity}/{UseCase}/{UseCase}CommandValidator.cs`

```csharp
using FluentValidation;

namespace Application.Todos.Complete;

public class CompleteTodoCommandValidator : AbstractValidator<CompleteTodoCommand>
{
    public CompleteTodoCommandValidator()
    {
        RuleFor(c => c.TodoItemId).NotEmpty();
    }
}
```

Runs automatically via `ValidationDecorator` (`Application/Abstractions/Behaviors/ValidationDecorator.cs`)
— the handler never re-validates input shape. Queries don't get a validator; the decorator only
wraps commands.

## Query + Handler (read), with a Response DTO

`src/backend/Application/{Entity}/{UseCase}/{UseCase}Query.cs` — same idea, `IQuery<TResponse>`.
Project straight to a `Response` DTO with `.Select(...)`; never return the Domain entity.
`Application/Todos/TodoResponse.cs` is the pattern to copy — it lives at the **feature root**
(`Application/{Entity}/{Entity}Response.cs`), not inside one query's folder, because more than one
query for the same entity usually returns the identical shape (here, both `Get` and `GetById` do).
Put a response DTO in `{Entity}Response.cs` in the query's own folder only if that query's shape is
genuinely different from every other query on the entity — don't create a second copy of the same
DTO just because it's convenient to keep it next to one query.

Cache a hot read with `HybridCache` (see `GetTodoByIdQueryHandler`) using a
`{Feature}CacheKeys` static class (`Application/Todos/TodoCacheKeys.cs`) — and invalidate that same
key in every command that mutates the cached row (see the handler above).

## Endpoint

`src/backend/Web.Api/Endpoints/{Entity}/{UseCase}.cs`

```csharp
using Application.Abstractions.Messaging;
using Application.Todos.Complete;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Todos;

internal sealed class Complete : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("todos/{id:guid}/complete", async (
            Guid id,
            ICommandHandler<CompleteTodoCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CompleteTodoCommand(id);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .ProducesProblemResponses()
        .WithTags(Tags.Todos)
        .HasPermission(PermissionNames.TodosAccess);
    }
}
```

**`.ProducesProblemResponses()` is mandatory on every endpoint that can fail** — see the "one rule"
callout in the main `SKILL.md`. Add `.Produces<T>()` above it when the success response has a body
(`Result<T>`); a bare `Result` success maps to `Results.NoContent` and needs no `.Produces<T>()`.

**Every endpoint needs `.HasPermission(PermissionNames.X)`** — see
`Web.Api/Endpoints/Users/GetAll.cs`. There is no "any authenticated user" tier: a bare
`.RequireAuthorization()` fails `IntegrationTests/EndpointAuthorizationTests`, because a valid token
from the tenant is not the same thing as a person who works here (it also covers every registered
service principal).

`PermissionNames` (`SharedKernel/PermissionNames.cs`) is referenced directly, with a plain
`using SharedKernel;` — there's no per-feature re-export class in between. A new permission needs
two edits: the constant there, and the role → permission mapping in
`Infrastructure/Authorization/PermissionProvider.cs`. Skip the second and the permission exists but
nobody can ever hold it.

If the request needs fields the route doesn't supply, add a nested `Request` record/class (see
`Web.Api/Endpoints/Todos/Create.cs`) and map it to the command inside the handler lambda — endpoints
never call into more than one command/query handler, and never contain business logic.

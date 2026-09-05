---
name: add-tests
description: Backfill missing tests for existing use cases in this Clean Architecture + Angular template — Application-layer handler unit tests, FluentValidation validator tests, HTTP integration tests against a real SQL Server container, and (since none exist yet) an Angular/Jasmine testing baseline for frontend components. Use when the user asks to add, improve, or backfill test coverage, backend or frontend.
argument-hint: <use case, feature, or component to cover, e.g. "CompleteTodoCommand" or "the todos page">
---


# Add Tests

This template has three backend test projects with different jobs — don't put a test in the wrong
one. There is currently **no** Angular test coverage at all (Karma/Jasmine are configured in
`package.json`, just unused) — see the frontend section if that's what's being asked for.

## Backend: which project does this test belong in?

| Testing... | Project | Real dependencies? |
|---|---|---|
| A command/query handler's logic | `tests/Application.UnitTests` | No — in-memory `TestDbContext`, everything else `Substitute.For<T>()` |
| A FluentValidation validator | `tests/Application.UnitTests` | No |
| Layer boundaries (Domain → Application → Infrastructure → Web.Api) | `tests/ArchitectureTests` | No — `NetArchTest` over compiled assemblies |
| An HTTP endpoint end to end | `tests/IntegrationTests` | Yes — real SQL Server via Testcontainers |

## 1. Handler unit tests (`Application.UnitTests`)

1. Open the target handler under `Application/{Entity}/{UseCase}/{UseCase}CommandHandler.cs` (or
   `...QueryHandler.cs`). List every `return Result.Failure(...)` guard clause plus the happy path.
2. Inherit `BaseHandlerTest` (`tests/Application.UnitTests/Abstractions/BaseHandlerTest.cs`).
   `CreateDbContext()` returns a fresh in-memory `TestDbContext` — **not** the real
   `ApplicationDbContext**, and there is no `IDomainEventsDispatcher` here (that dispatch only
   happens in the real `Infrastructure.Database.ApplicationDbContext.SaveChangesAsync` override).
   Assert domain events by reading `entity.DomainEvents` directly off the persisted entity, not by
   verifying a dispatcher call.
3. Substitute every interface the handler takes: `IUserContext`, `IDateTimeProvider`, etc., via
   `Substitute.For<T>()`. Never substitute `IApplicationDbContext` — use the real `TestDbContext`
   so EF Core's actual query behavior is exercised.
   For `IUserContext`, stub all three members the handler might read: `UserId` (the **local** user
   id, not a directory `oid`), `TenantId`, and `Roles` — an authorization branch like
   `userContext.Roles.Contains(RoleNames.Admin)` silently takes the deny path when `Roles` is left
   as NSubstitute's default empty sequence, which is easy to mistake for a real failure.
4. One test per failure path, one happy-path test asserting persisted state (`context.TodoItems.SingleAsync(...)`)
   and, for commands, that the entity's `DomainEvents` contains the expected event type.

```csharp
using Application.Abstractions.Authentication;
using Application.Todos.Complete;
using Application.UnitTests.Abstractions;
using Domain.Todos;
using SharedKernel;

namespace Application.UnitTests.Todos;

public sealed class CompleteTodoCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTodoDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        HybridCache cache = CreateCache();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var handler = new CompleteTodoCommandHandler(context, dateTimeProvider, userContext, cache);

        // Act
        Result result = await handler.Handle(new CompleteTodoCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TodoItemErrors.NotFound(/* the id used above */ default));
    }
}
```

Global usings already cover `Xunit`, `NSubstitute`, `Shouldly` — no need to add them per file.

## 2. Validator tests (`Application.UnitTests`, commands only)

One test per FluentValidation rule (asserting the failure) plus one fully-valid command, using
`FluentValidation.TestHelper`. Group every validator for a feature into one
`{Feature}ValidatorsTests` class — see `Application.UnitTests/Todos/TodoValidatorsTests.cs`. Direct
instantiation, no DI: `new CompleteTodoCommandValidator()`.

## 3. Integration tests (`IntegrationTests`)

Inherit `BaseIntegrationTest(factory)` with the `IntegrationTestCollection` fixture (already applied
via `[Collection(nameof(IntegrationTestCollection))]` on the base class).

There is no login: real Entra ID tokens cannot be acquired in CI, so the suite runs the API's
development authentication scheme and varies the caller through `X-Dev-*` headers. The base class
wraps that in four factories — pick the one that states the case you mean:

| Factory | Who it is |
|---|---|
| `CreateAdminClient()` | holds `Admin` + `Member` |
| `CreateMemberClient(out Guid objectId)` | an ordinary assigned user, brand-new `oid` |
| `CreateUnassignedClient()` | authenticated against the tenant, **no App Role** → every endpoint must 403 |
| `CreateAnonymousClient()` | no principal at all → 401 |

Each client invents a fresh `oid`, so its first call also exercises just-in-time provisioning for
real. To learn the caller's **local** user id — the one that goes in request bodies and foreign
keys — call `GET /users/me`; the token only carries the directory's `oid`, and `GET /todos` needs
`?userId=` to be that local id.

Assert state via a follow-up GET, not by reaching into the database. Define a private response DTO
record inside the test class for deserialization — integration tests go through real HTTP and don't
reference `Application`/`Domain` types.

Cover: the happy path with a follow-up GET confirming persisted state; each documented failure
(404/409/403/400) your handler can return; **403 for `CreateUnassignedClient()`**; and 401 for
`CreateAnonymousClient()` if this is a new route family. Docker must be running — this spins up a
real, throwaway SQL Server container per test collection via `Testcontainers.MsSql`.

## 4. Run everything

```bash
dotnet test CleanArchitecture.sln
```

## 5. Frontend (Angular/Jasmine) — there is no existing pattern to mirror yet

`ng test` (Karma + Jasmine, `karma.conf.js`-less default Angular CLI setup) is wired in
`package.json` but nothing under `src/frontend/src/app` has a `.spec.ts` yet. If you're asked to add
frontend coverage, establish the pattern rather than searching for one:

- Use Angular's standard `TestBed` + standalone component harness (`TestBed.configureTestingModule({ imports: [YourComponent] })`, since every component here is standalone — no `NgModule` to declare it in).
- **Never let a test touch the real `ApiClientService`** — it constructs a live Kiota client against
  `API_BASE_URL`. Provide a stub in the `TestBed` providers list instead:
  `{ provide: ApiClientService, useValue: { client: fakeClientObject } }`, shaping `fakeClientObject`
  to match only the methods the component under test actually calls.
- Test what the component does with `signal()` state transitions (loading → data / loading → error)
  given a resolved vs. rejected fake client call, not Kiota's internals — those are out of scope,
  Kiota's generated code isn't yours to test.
- Naming: `{component-name}.component.spec.ts`, next to the component file, matching Angular CLI
  convention (`ng generate component` would place it there by default).

Run with `cd src/frontend && npm test`.

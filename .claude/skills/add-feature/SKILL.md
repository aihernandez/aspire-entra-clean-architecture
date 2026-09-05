---
name: add-feature
description: Scaffold a complete feature across this Clean Architecture template's backend layers (Domain, Application, Infrastructure, Web.Api) and, when the user wants it, the Angular frontend too — CQRS command/query + handler + FluentValidation validator, a minimal API endpoint with typed ProblemDetails responses, the Kiota client regeneration step, and an Angular page wired to the generated client. Use when the user asks to add a feature, use case, command, query, or endpoint (backend, frontend, or both) to this Clean Architecture + Angular template.
argument-hint: <feature description, e.g. "archive a todo item" or "let a user download their data as CSV">
---


# Add a Feature

A feature here is a **use case that spans layers**, not a single file — the opposite of a
Vertical Slice template. One command/query lives in its own folder under `Application/{Entity}/{UseCase}/`,
its own file per concern (`{UseCase}Command.cs`, `{UseCase}CommandHandler.cs`,
`{UseCase}CommandValidator.cs`), and the HTTP endpoint lives entirely separately, under
`Web.Api/Endpoints/{Entity}/{UseCase}.cs`. No MediatR — this codebase uses its own `ICommand`/`IQuery`
abstractions (`Application.Abstractions.Messaging`) with Scrutor-registered handlers and decorators.

## Workflow

1. **Classify the use case.** A state change is a **command**; a read is a **query**. Name it
   `{Verb}{Entity}Command`/`Query`, e.g. `ArchiveTodoCommand`, `GetOverdueTodosQuery`.
2. **Check the entity exists.** If the Domain entity, its `{Entity}Errors`, or a needed domain
   event doesn't exist yet, run the `add-entity` skill first.
3. **Build the backend slice** — Application (command/query + handler + validator) and Web.Api
   (endpoint). Full templates and non-negotiable conventions: [references/backend.md](references/backend.md).
4. **If the feature needs frontend work**, regenerate the Kiota client and wire an Angular page to
   it. Full workflow and known pitfalls: [references/frontend.md](references/frontend.md).
5. **Write tests** for whatever you built — hand off to the `add-tests` skill, or write them
   inline following its conventions.
6. **Verify:** `dotnet build CleanArchitecture.sln` then `dotnet test CleanArchitecture.sln`
   (Docker must be running for the integration tests). If you touched the frontend, also
   `cd src/frontend && npm run build`.

## The one rule that's easy to skip and breaks the frontend silently

Every new endpoint that can fail **must** call `.ProducesProblemResponses()` (from
`Web.Api.Extensions.EndpointExtensions`) alongside `.Produces<T>()`. Without it, the OpenAPI
document doesn't declare error response shapes, so Kiota generates a client where every failure
throws a bare, bodiless error — no `.title`/`.detail`, nothing to show the user. This is not
optional polish; it's the difference between a frontend that can show *why* something failed and
one that can only say "something went wrong." See [references/backend.md](references/backend.md)
for where it goes.

## Non-negotiable conventions (backend)

- **One concern per file**, all under `Application/{Entity}/{UseCase}/`: `{UseCase}Command.cs` (or
  `Query.cs`), `{UseCase}CommandHandler.cs`, `{UseCase}CommandValidator.cs` (commands only).
- **Handlers are `internal sealed`** with primary constructors, implementing `ICommandHandler<T>`,
  `ICommandHandler<T, TResponse>`, or `IQueryHandler<T, TResponse>` (from `Application.Abstractions.Messaging`).
- **No manual DI registration.** Handlers and validators are discovered by Scrutor scanning
  (`Application/DependencyInjection.cs`) and `AddValidatorsFromAssembly(includeInternalTypes: true)`.
  Endpoints are discovered by `AddEndpoints` (`Web.Api/Extensions/EndpointExtensions.cs`). Never
  wire a new slice up by hand.
- **Return `Result` / `Result<T>`, never throw** for expected failures. Use the entity's static
  `{Entity}Errors` factory. This template has `Error.Forbidden` (403) in addition to the usual
  `NotFound`/`Conflict`/`Problem`/`Failure` — use it for "not allowed", not `Error.Failure` (500).
- **Handlers depend only on `Application.Abstractions.*` interfaces** — `IApplicationDbContext`,
  `IUserContext`, `IDateTimeProvider`, `IEmailSender`/`IEmailService` — never on EF Core's
  `DbContext`, `Microsoft.Identity.Web`, or MailKit directly. `HybridCache` is the one exception:
  it's injected directly into handlers that need caching (see `TodoCacheKeys` /
  `GetTodoByIdQueryHandler`), not wrapped in a custom interface.
- **The caller comes from `IUserContext`**: `UserId` (the **local** `Domain.Users.User.Id`, not the
  directory's `oid`), `TenantId`, and `Roles` (the App Roles carried by the access token). Read
  users from `IApplicationDbContext.Users` like any other entity — there is no identity service any
  more, and no directory call.
- **Never check "is this user an admin" against the database.** Roles live in the token:
  `userContext.Roles.Contains(RoleNames.Admin)`. Querying the `Users` table for authorization
  reintroduces the local role store Entra ID replaced (`EntraIdMigration.md`, D4).
- **Never assume a user row might be missing** for the caller. Reaching a handler means the request
  carried an App Role, and the provisioning middleware has already created or loaded that row.
- **Endpoints** implement `IEndpoint` (`Web.Api.Endpoints`), are `internal sealed`, resolve the
  handler interface directly from DI, and call `.Produces<T>()`, `.ProducesProblemResponses()`,
  `.WithTags(Tags.X)` and `.HasPermission(PermissionNames.X)` — the constant comes straight from
  `SharedKernel.PermissionNames`, no per-feature re-export in between (see
  `Web.Api/Endpoints/Users/GetAll.cs`).
- **`.HasPermission(...)` is required on every endpoint. A bare `.RequireAuthorization()` is a
  build failure**, caught by `IntegrationTests/EndpointAuthorizationTests`. "Any authenticated
  caller" is not a real audience: it admits every service principal registered in the tenant, not
  just the people who work there. If the feature needs a new permission, add the constant to
  `SharedKernel/PermissionNames.cs` **and** map it from a role in
  `Infrastructure/Authorization/PermissionProvider.cs` — a permission no role grants is dead.

## Naming reference

| Artifact | Pattern | Example |
|---|---|---|
| Command folder | `Application/{Entity}/{UseCase}/` | `Application/Todos/Complete/` |
| Command/Query | `{UseCase}Command.cs` / `{UseCase}Query.cs` | `CompleteTodoCommand` |
| Handler | `{UseCase}CommandHandler.cs` / `{UseCase}QueryHandler.cs` | `CompleteTodoCommandHandler` |
| Validator | `{UseCase}CommandValidator.cs` (commands only) | `CompleteTodoCommandValidator` |
| Response DTO | `{Entity}Response.cs`, at the feature root (shared across queries) | `Application/Todos/TodoResponse.cs` |
| Endpoint | `Web.Api/Endpoints/{Entity}/{UseCase}.cs` | `Web.Api/Endpoints/Todos/Complete.cs` |
| Unit test | `{UseCase}CommandHandlerTests` / `{UseCase}QueryHandlerTests` | `CompleteTodoCommandHandlerTests` |
| Test method | `Handle_Should_{Outcome}_When{Condition}` | `Handle_Should_ReturnNotFound_WhenTodoDoesNotExist` |

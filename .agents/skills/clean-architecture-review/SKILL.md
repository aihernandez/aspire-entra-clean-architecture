---
name: clean-architecture-review
description: Review pending changes against this template's conventions — layer boundaries (Domain/Application/Infrastructure/Web.Api), the IApplicationDbContext abstraction, ProducesProblemResponses on every endpoint, permission-based authorization, Kiota client freshness, and typed error handling in Angular. Use when the user asks to review changes, check conventions, or audit a feature before committing, in this Clean Architecture + Angular template.
argument-hint: [optional: specific files or feature to review; defaults to the working-tree diff]
---


# Clean Architecture + Angular Convention Review

Review the given scope (default: `git diff` + untracked files) against this template's
conventions. Report findings with file:line references, ordered by severity. Do not fix anything
unless asked.

## Checklist

### Layer boundaries (violations are blockers)

- `Domain` has zero dependencies on `Application`, `Infrastructure`, or `Web.Api`. No EF Core, no
  `Microsoft.Identity.Web`, no `Microsoft.Extensions.*` types on a Domain entity.
- `Application` depends only on `Domain`, `SharedKernel`, and its own `Application.Abstractions.*`
  interfaces (`IApplicationDbContext`, `IUserContext`, `IEmailSender`/`IEmailService`,
  `IDateTimeProvider`). Flag any direct reference to `Microsoft.EntityFrameworkCore`
  (beyond LINQ extension methods like `.Where`/`.Select`/`.SingleOrDefaultAsync`, which are fine —
  the *type* `DbContext` itself is what must not appear), `Microsoft.Identity.Web`, or
  `MailKit` inside `Application`. `Microsoft.Extensions.Caching.Hybrid.HybridCache` is the one
  sanctioned exception — it's injected directly, not wrapped.
- `Infrastructure` implements `Application`'s interfaces; it must never be referenced *by*
  `Application` or `Domain`. `Web.Api` may reference both.
- These four rules are executable, not just convention — `tests/ArchitectureTests/Layers/LayerTests.cs`
  runs `NetArchTest` assertions for exactly this. If a review finds a violation, `dotnet test
  tests/ArchitectureTests` should already be red; if it isn't, the test itself is missing coverage
  for the new code and that's also a finding.

### Application layer structure

- One file per concern under `Application/{Entity}/{UseCase}/`: `{UseCase}Command.cs`/`Query.cs`,
  `{UseCase}CommandHandler.cs`, `{UseCase}CommandValidator.cs` (commands only). Flag a handler and
  its command sharing a file, or a use case's files scattered outside its `{UseCase}/` folder.
- Handlers are `internal sealed` with primary constructors, implementing
  `ICommandHandler<T>`/`ICommandHandler<T, TResponse>`/`IQueryHandler<T, TResponse>`
  (`Application.Abstractions.Messaging`).
- No manual DI registration for handlers/validators — Scrutor scanning
  (`Application/DependencyInjection.cs`) and `AddValidatorsFromAssembly` must find them by
  convention. Any hand-written `services.AddScoped<ICommandHandler<...>, ...>()` is a red flag.
- Queries project to a `Response` record with `.Select(...)`; never return a Domain entity across
  the handler boundary.

### Error handling

- Expected failures return `Result`/`Result<T>` — no exceptions for control flow.
- Errors come from static `{Entity}Errors` factories in `Domain/{Entity}/{Entity}Errors.cs`, codes
  `"{Feature}.{Reason}"`. Check the factory matches the real semantics: `Error.Forbidden` (403) for
  "not allowed", not `Error.Failure` (500) — this template has all five `ErrorType`s
  (`Failure`/`Validation`/`Problem`/`NotFound`/`Conflict`/`Forbidden`); using the wrong one silently
  changes the HTTP status code a client sees.
- Endpoints translate failures only via `result.Match(Results.Ok|NoContent, CustomResults.Problem)`.

### Endpoints — the Kiota contract

- **Every endpoint that can fail calls `.ProducesProblemResponses()`** (`Web.Api.Extensions.EndpointExtensions`)
  alongside `.Produces<T>()`. This is the single most impactful thing to check — an endpoint missing
  it still works over plain HTTP, but Kiota generates a client where its failures throw a bare,
  bodiless error, silently degrading every frontend that calls it. As of this template's current
  state, every `Users` endpoint has it and every `Todos` endpoint does not — treat any endpoint
  missing it as a real finding, not a style nit, and check whether the frontend for that endpoint
  is (or should be) using `extractErrorMessage` (`src/frontend/src/app/core/api-error.ts`) once it's
  added.
- Endpoints are `internal sealed`, implement `IEndpoint`, tagged (`.WithTags(Tags.X)`), and carry
  `.HasPermission(PermissionNames.X)`. **A bare `.RequireAuthorization()` is a blocker**, not a
  style nit — it means "any valid token", which includes every service principal registered in the
  tenant. `IntegrationTests/EndpointAuthorizationTests` fails the build for it. Endpoints contain
  only request→command mapping and result matching — flag any business logic in the lambda.
- If a Web.Api endpoint changed shape (route, request/response, new error) and the client wasn't
  regenerated afterward (`./scripts/generate-api-client.ps1` — which also syncs it into
  `src/frontend/src/app/api-client/`), the frontend is building against a stale contract — check
  the diff for both sides changing together. If someone ran `dotnet kiota generate` by hand instead
  of the script, `clients/api-client/` may be current while Angular's synced copy isn't — diff the
  two folders if that's suspected.

### Persistence — the three-place DbSet rule

- A new entity's `DbSet` must exist in all three of: `Application/Abstractions/Data/IApplicationDbContext.cs`,
  `Infrastructure/Database/ApplicationDbContext.cs`, and `tests/Application.UnitTests/Abstractions/TestDbContext.cs`.
  Missing the interface breaks Application handlers; missing the real context is a silent
  runtime-only gap (compiles, table never mapped); missing `TestDbContext` breaks the test project
  the first time someone writes a handler test — check for all three whenever `Infrastructure/{Entity}/{Entity}Configuration.cs`
  is new.
- Relationships are shadow-style FKs (`HasOne<User>().WithMany()`), not navigation properties, and
  a user relationship references `Domain.Users.User` — the application's own record — keyed by its
  **local** `Id`. Flag any foreign key or stored value holding a directory `oid` instead.

### Security

- Handlers acting on user-owned data enforce ownership via `IUserContext.UserId` — either filtering
  the query by it directly, or comparing against a caller-supplied id and returning
  `UserErrors.Unauthorized()` on mismatch.
- **Authorization reads the token, never the database.** Role checks go through
  `IUserContext.Roles` (App Roles from the `roles` claim). A `SELECT` against the `Users` table to
  decide what someone may do is a blocker: it reintroduces the local role store Entra ID replaced
  (`EntraIdMigration.md`, D3/D4). `PermissionProvider` must stay a pure function — no DB, no
  `async`.
- A new `PermissionNames` constant is dead unless some role grants it in
  `Infrastructure/Authorization/PermissionProvider.cs`. Check both sides changed together.
- Nothing keys user data on `email`, `preferred_username` or `upn`. Those are display values a
  tenant administrator can change and reassign; the identity key is the `(oid, tid)` pair, and only
  `Domain.Users.User` should hold it.
- Permission-gated endpoints reference `PermissionNames.X` (`SharedKernel/PermissionNames.cs`)
  directly, not a magic string — and there should be no per-feature re-export class reintroducing
  an indirection that was deliberately removed (see `Web.Api/Endpoints/Users/GetAll.cs` for the
  direct-reference pattern).
- No secrets, connection strings with real credentials, or personal email addresses committed in
  `appsettings.Development.json` or anywhere else about to be pushed.
- **`DevelopmentAuthentication` must not appear in any configuration other than
  `appsettings.Development.json`.** That section bypasses authentication entirely; startup refuses
  it elsewhere, but a diff that adds it to a Production or Staging file is a blocker regardless.
  Likewise flag any change that registers the development scheme outside `IsDevelopment()`.

### Frontend

- New pages are standalone components using `signal()` for state and `inject()` for DI, calling
  the API only through `inject(ApiClientService).client` — never the raw generated client type
  (it's a `Proxy` that crashes Angular's DI if provided directly).
- Errors are surfaced via `extractErrorMessage(error, fallback)`, not a bare
  `catch { this.error.set('generic string') }` that discards the backend's real message.
- New routes needing auth are nested under the `canActivate: [authGuard]` parent route in
  `app.routes.ts`, using `loadComponent` lazy imports like every existing route.

### Tests

- New/changed Application handlers have unit tests in `Application.UnitTests` covering every
  `Result.Failure` path plus the happy path (persisted state + `entity.DomainEvents`), using
  `TestDbContext` — not the real `ApplicationDbContext`, and no `IApplicationDbContext` mock.
- New/changed validators have `TestValidate` coverage per rule.
- New/changed endpoints have `IntegrationTests` coverage over real HTTP (Testcontainers SQL Server).
- New layer-crossing code has architecture-test coverage if it introduces a boundary that
  `LayerTests.cs` doesn't already assert.

## Output format

Group findings as **Blockers** (layer-boundary violations, `IApplicationDbContext` bypassed, thrown
exceptions for expected failures, missing auth), **Convention violations** (missing
`.ProducesProblemResponses()`, incomplete three-place `DbSet` wiring, naming, error-type mismatches,
stale Kiota client), and **Test gaps**. For each: `file:line`, what's wrong, the one-line fix. Close
with a verdict: ready to commit, or what must change first. If everything passes, say so and run
`dotnet build CleanArchitecture.sln` + `dotnet test CleanArchitecture.sln` to confirm.

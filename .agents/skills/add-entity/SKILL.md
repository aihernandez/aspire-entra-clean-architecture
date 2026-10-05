---
name: add-entity
description: Add a new domain entity to this Clean Architecture template — Domain entity, error catalog, domain events, Infrastructure EF Core configuration, a migration, and DbSet wiring across all three places that need it (IApplicationDbContext, the real ApplicationDbContext, and the unit-test TestDbContext). Use when the user asks to add an entity, aggregate, domain model, or table.
argument-hint: <entity description, e.g. "Project with a name and owner">
---


# Add a Domain Entity

Create a new entity and wire it through all four backend layers, following the `TodoItem`
pattern (`src/backend/Domain/Todos/TodoItem.cs`). Unlike a single-project template, persistence
here is behind `IApplicationDbContext` — a new entity's `DbSet` has to be added in **three**
places, not one. Missing any of them is the #1 way this goes wrong.

## Files to create/modify

1. **Entity** — `src/backend/Domain/{Entity}/{Entity}.cs`

```csharp
using SharedKernel;

namespace Domain.Projects;

public sealed class Project : Entity
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Name { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

`sealed class`, inherits `Entity` (from `SharedKernel` — gives it `DomainEvents` + `Raise(...)`),
`Guid Id`, plain settable properties, collections initialized with `= [];`. No EF attributes, no
persistence concerns — this project must never reference EF Core, Entra ID / `Microsoft.Identity.Web`,
or anything from `Infrastructure`.

2. **Error catalog** — `src/backend/Domain/{Entity}/{Entity}Errors.cs`

```csharp
using SharedKernel;

namespace Domain.Projects;

public static class ProjectErrors
{
    public static Error NotFound(Guid projectId) => Error.NotFound(
        "Projects.NotFound",
        $"The project with the Id = '{projectId}' was not found");
}
```

Codes are `"{FeaturePlural}.{Reason}"`. Pick the factory by semantics — this template has **five**
`ErrorType`s, not the usual four: `Error.NotFound` (404), `Error.Conflict` (409), `Error.Problem`
(400), `Error.Forbidden` (403 — use this for "you're not allowed to do this", not `Error.Failure`),
`Error.Failure` (500, reserve for genuinely unexpected failures). `Error`/`ErrorType` live in
`SharedKernel`.

3. **Domain events** — one record per file, `src/backend/Domain/{Entity}/{Entity}{PastTenseVerb}DomainEvent.cs`

```csharp
using SharedKernel;

namespace Domain.Projects;

public sealed record ProjectCreatedDomainEvent(Guid ProjectId) : IDomainEvent;
```

Create at minimum the `Created` event; add others as commands need them. Events carry ids, not
entities. `IDomainEvent` is in `SharedKernel`.

4. **EF configuration** — `src/backend/Infrastructure/{Entity}/{Entity}Configuration.cs`

```csharp
using Domain.Projects;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Projects;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasKey(p => p.Id);

        builder.HasOne<User>().WithMany().HasForeignKey(p => p.OwnerId);
    }
}
```

Relationships are shadow-style (`HasOne<User>().WithMany()`) — entities hold foreign-key ids, not
navigation properties. **A user reference points at `Domain.Users.User`**, the application's own
slim user record, and its **local** `Id` — never the directory's `oid`. Entra ID owns identity, but
no foreign key can point at a directory, which is exactly why that local row exists (see
`EntraIdMigration.md`, D3). Configurations are picked up automatically by
`ApplyConfigurationsFromAssembly` in `ApplicationDbContext.OnModelCreating`.

5. **`DbSet` wiring — three places, all required:**

   a. **The interface** `src/backend/Application/Abstractions/Data/IApplicationDbContext.cs` — add
      `DbSet<Project> Projects { get; }`. This is the only view of persistence the `Application`
      layer is allowed to see.

   b. **The real DbContext** `src/backend/Infrastructure/Database/ApplicationDbContext.cs` — add
      `public DbSet<Project> Projects { get; set; }`. This is a plain `DbContext`: it declares
      `Users` and `TodoItems` explicitly and there is no Identity base class bringing tables along
      with it.

   c. **The test double** `tests/Application.UnitTests/Abstractions/TestDbContext.cs` — add
      `public DbSet<Project> Projects { get; set; }` here too. Application-layer unit tests build an
      in-memory `TestDbContext` (it implements `IApplicationDbContext` directly) instead of the real
      `ApplicationDbContext`, specifically so `Application.UnitTests` never has to reference
      `Infrastructure`. Forgetting this step means any new handler test for this entity won't compile.

   Skipping (a) breaks every Application handler that needs the entity; skipping (b) is a runtime
   error only (compiles, but the table is never mapped); skipping (c) is a test-project compile
   error the first time someone writes a handler test for it. All three are easy to forget
   independently — do them together, right after the EF configuration.

6. **Migration** — from the repo root:

```bash
dotnet ef migrations add Add_{Plural} --project src/backend/Infrastructure --startup-project src/backend/Web.Api
```

Migration names are `PascalCase_With_Underscores` (see `InitialCreate`, or `Add_Projects`). The
`--project`/`--startup-project` split matters here (unlike a single-project template) — the
`DbContext` lives in `Infrastructure`, but `Web.Api` is what EF's tooling needs to build to read
configuration/connection strings.

## Rules

- Domain entity, errors, and domain events go in `Domain/{Entity}/` (namespace `Domain.{Entity}`);
  the EF configuration goes in `Infrastructure/{Entity}/` (namespace `Infrastructure.{Entity}`).
  Never put persistence attributes or EF Core types on the Domain entity itself.
- `Domain` must never reference `Application`, `Infrastructure`, or `Web.Api` —
  `tests/ArchitectureTests/Layers/LayerTests.cs` enforces this and will fail the build if violated.
- Domain types are shareable across features (a Project could reference `User`'s id, or
  another feature could reference `Project`), but Application-layer command/query/handler code from
  one feature must never reference another feature's handlers.
- Run `dotnet build CleanArchitecture.sln` then `dotnet test CleanArchitecture.sln` when done —
  the architecture tests alone catch most layering mistakes immediately.
- If the user also wants use cases (CRUD, etc.) for the entity, continue with the `add-feature`
  skill.

# Agent Skills for Claude Code

A skill pack that teaches Claude Code the conventions of this Clean Architecture + Angular
template — so every feature it builds crosses the four backend layers correctly, keeps the Kiota
client and the Angular frontend in sync with the API, and looks like you wrote it by hand.

## What's inside

| Skill | Invoke with | What it does |
|---|---|---|
| **add-entity** | `/add-entity Project with a name and owner` | Adds a Domain entity end to end: entity, error catalog, domain events, EF configuration, and `DbSet` wiring across all three places that need it (`IApplicationDbContext`, `ApplicationDbContext`, `TestDbContext`), plus a migration. |
| **add-feature** | `/add-feature archive a todo item` | Scaffolds a full use case across `Application` (command/query, handler, validator) and `Web.Api` (endpoint with typed `ProblemDetails` responses) — and, on request, regenerates the Kiota client and wires an Angular page to it. |
| **add-tests** | `/add-tests CompleteTodoCommand` | Backfills handler unit tests, validator tests, and HTTP integration tests for an existing use case — or establishes an Angular/Jasmine testing baseline, since none exists yet. |
| **clean-architecture-review** | `/clean-architecture-review` | Reviews pending changes against this template's conventions: layer boundaries, `IApplicationDbContext` isolation, `ProducesProblemResponses` on every endpoint, permission wiring, Kiota freshness, and typed frontend error handling. |

You don't have to invoke them explicitly — once installed, Claude Code lists them (with their
descriptions) at the start of every session and is expected to pick the matching one automatically
when a request fits it, e.g. "add support for tracking invoices" → `add-entity`, then
`add-feature`. Auto-triggering isn't a hard guarantee, though — it depends on how closely the
request's wording matches a skill's `description`. If a skill doesn't fire when you expect it to,
invoke it explicitly with `/skill-name` rather than assuming it silently ran.

This only applies inside Claude Code (or another tool with the same skill-discovery mechanism). A
teammate using a different AI assistant won't have these auto-loaded — the `SKILL.md` files are
still plain Markdown they can open and follow, or paste into their tool's own instructions/rules
mechanism if it has one.

## Installation

The skills live in `.claude/skills/`. If you cloned this repo, they're already active — just open
it in Claude Code.

To use them in another project based on this template, copy the folder:

```
your-project/
└── .claude/
    └── skills/
        ├── add-entity/
        ├── add-feature/
        │   └── references/
        │       ├── backend.md
        │       └── frontend.md
        ├── add-tests/
        └── clean-architecture-review/
```

If your fork renames the solution, the `Web.Api`/`Application`/`Infrastructure`/`Domain`/
`SharedKernel` projects, or the Angular app's folder, update the paths and namespaces inside each
`SKILL.md` — they're written against this template's exact layout, not a generic placeholder.

## Try it

```
/add-feature let a user snooze a todo until a given date
```

Claude will scaffold the `Application/Todos/Snooze/` command + handler + validator, the
`Web.Api/Endpoints/Todos/Snooze.cs` endpoint (with `.ProducesProblemResponses()`), ask whether you
also want the Angular side, and — if so — regenerate the Kiota client and wire a control into the
todos page using the typed error helper. Follow up with `/add-tests SnoozeTodoCommand` for
coverage, and `/clean-architecture-review` before committing.

## Customizing

Each skill is a plain Markdown file (`SKILL.md`, plus templates under `add-feature/references/`).
Want a different response-DTO convention, a different Angular state-management approach, or to swap
Kiota for hand-written HTTP calls? Edit the templates once and every future feature follows suit.
The skills are the executable version of your team's conventions doc — and a place to fold in the
next entry from `LESSONS-LEARNED.md` the moment a new pitfall gets discovered, so nobody hits it
twice.

---

Backend template derived from [amantinband/clean-architecture](https://github.com/amantinband/clean-architecture)
(Copyright (c) 2023 Amichai Mantinband), MIT-licensed — see the repo root [LICENSE](../../LICENSE).

# Frontend wiring (Angular + Kiota)

Only needed when the feature has a UI. Do the backend first (see `references/backend.md`) — the
Kiota client is generated from the backend's OpenAPI document, so there's nothing to regenerate
against until the endpoint exists and builds.

## 1. Regenerate the typed API client

```powershell
./scripts/generate-api-client.ps1
```

This builds `Web.Api` (exports `src/backend/Web.Api/obj/openapi/Web.Api.json` at build time via
`Microsoft.Extensions.ApiDescription.Server`), runs `dotnet kiota generate` against it into
`clients/api-client/` — gitignored generated output, framework-agnostic by design — and then
**syncs that output into every frontend that consumes it**, currently
`src/frontend/src/app/api-client/` (also gitignored). That sync step is what Angular actually
builds against; running `dotnet kiota generate` directly instead of this script leaves Angular on a
stale client with no error, so always use the script. **Never hand-edit** anything under either
`api-client/` folder — add a new sync target to the script's `$consumers` array if a future
frontend (e.g. a React app) needs the same client, rather than copying it by hand.

Two known Kiota TypeScript (preview) quirks — open the generated file for your new endpoint/model
and check the real type before writing code against it, don't assume:

- **Query parameters formatted `int32`** (e.g. `?pageNumber=1`) come out typed as `string` in the
  generated `...QueryParameters` interface — pass `String(value)`, not the number.
- **Model properties formatted `int32`** (e.g. `PagedResponse<T>.PageNumber`) come out as
  `UntypedNode`, not `number` — read the value via `.value`, or use the `untypedNumber()` helper in
  `src/frontend/src/app/core/kiota-untyped.ts`.

If the endpoint you added is missing `.ProducesProblemResponses()` (see `references/backend.md`),
regenerating will still succeed, but every failure from that endpoint throws a bare error with no
`.title`/`.detail` — go back and add it before writing frontend error handling, or you'll be
debugging a symptom instead of the cause.

## 2. Call the client from Angular

Inject the wrapper service, never the raw generated client type:

```typescript
private readonly apiClient = inject(ApiClientService).client;
```

`ApiClientService` (`core/api-client.provider.ts`) wraps Kiota's generated client specifically so
Angular's DI never holds the client itself as a provided value — the client is a JS `Proxy` that
throws for any property it doesn't recognize as a navigation segment, **including `ngOnDestroy`**,
which Angular's injector probes on every provided value. Injecting the raw client crashes bootstrap
silently (blank page, no console output). Always go through the wrapper.

Endpoint calls follow the OpenAPI path as a fluent chain:
`apiClient.todos.byId(id).complete.put()` for `PUT /todos/{id}/complete`,
`apiClient.todos.get({ queryParameters: { userId } })` for `GET /todos?userId=...`. Match the
shape of the endpoint you just added in `Web.Api/Endpoints/`.

## 3. Handle errors with the typed ProblemDetails, not a generic catch

```typescript
import { extractErrorMessage } from '../core/api-error';

try {
  await this.apiClient.projects.post({ name: this.name });
} catch (error) {
  this.error.set(extractErrorMessage(error, 'Could not create the project.'));
}
```

`extractErrorMessage` (`core/api-error.ts`) pulls the backend's real `.detail`/`.title` out of the
deserialized `ProblemDetails` — including per-field FluentValidation messages — falling back to the
caller-supplied default only for genuinely unexpected failures (network error, an undeclared status
code). Every existing page except `todos-page.component.ts` already does this; a bare
`catch { this.error.set('generic string') }` throws away the real backend message for no reason —
don't copy that one, it predates this convention.

## 4. Component, route, and form validation

- Standalone component, `signal()` for state (`loading`, `busy`, `error`, plus the data itself),
  `inject()` for DI — no `NgModule`, no constructor injection. Copy the shape of
  `todos/todos-page.component.ts` + `.html`.
- Add a lazy route in `app.routes.ts`: `loadComponent: () => import('./{feature}/{feature}-page.component').then(m => m.{Feature}PageComponent)`.
  Nest it under the existing `canActivate: [authGuard]` parent route — every route does, since
  there are no unauthenticated pages: with Entra ID the sign-in screen is Microsoft's, and the
  guard redirects there rather than to a local login page.
- Mirror the backend's FluentValidation rules as HTML5/Angular template-driven validation
  (`required`, `[(ngModel)]`, `maxlength`, etc.) so the user gets instant feedback — but the backend
  validator is still the source of truth; don't skip step 3's error handling assuming the frontend
  check catches everything.
- Styling is Tailwind utility classes directly in the template, matching the existing pages'
  palette (slate for neutral text/borders, indigo for primary actions, red-50/red-700 for errors).

## 5. Verify

```bash
cd src/frontend
npm run build
```

There is currently no automated test coverage for Angular components in this repo (see the
`add-tests` skill for how to add some) — a successful build plus a manual click-through via
`dotnet run --project src/backend/Aspire.AppHost` is the available verification until then.

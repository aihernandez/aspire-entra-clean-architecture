# Angular frontend

This Angular 22 application calls the .NET API through a Kiota generated TypeScript client. The
generated files under `src/app/api-client/` are ignored by Git and must exist before building or
serving the frontend.

From the repository root, with .NET 10, Node.js, npm and Docker available:

```powershell
./scripts/setup-dev.ps1
dotnet run --project src/backend/Aspire.AppHost
```

The setup command installs the locked npm dependencies and generates the client from Web.Api.
Aspire starts Angular at `http://localhost:4200` with the API and local dependencies. If an API
contract changes, rerun `./scripts/generate-api-client.ps1` before compiling Angular.

To validate this app from `src/frontend`:

```powershell
npm run build
npm test -- --watch=false
```

The unit tests use Vitest through Angular CLI with jsdom and one thread worker configured in
`vitest-base.config.mts` for bounded resource use. They cover sign-in configuration
failures, shared current-user requests, retries, menu permissions and displayed roles. They
simulate a DOM; real Entra sign-in and browser redirects require a separate browser check.
Karma and Jasmine are no longer dependencies. Run `npm audit` to check all dependencies,
including development tools, and `npm audit --omit=dev` for the production dependency set.

Angular reads sign-in configuration from the API's `/auth-config` endpoint. In local development,
the API can use its development authentication scheme. A failed request or incomplete sign-in
configuration displays an error and does not enable that scheme in the browser.

# Entra ID setup

What to create in Microsoft Entra ID so this template authenticates real people, and what to put in
configuration afterwards. The *why* behind each choice is in [`EntraIdMigration.md`](./EntraIdMigration.md);
this file is the checklist.

**You do not need any of this to run the template.** With `AzureAd:ClientId` empty, the API uses its
development authentication scheme and the SPA skips MSAL entirely — `dotnet run` boots the whole
stack with no tenant. Do this when you are ready to authenticate for real.

You need permission to create app registrations, and someone who can grant admin consent and
assign app roles — **Application Developer** plus **Cloud Application Administrator**, or any role
that includes both.

---

## The short way

```powershell
az login
./scripts/setup-entra.ps1
```

That performs every step below through Microsoft Graph and stores the resulting ids in .NET user
secrets. Roughly fifteen portal clicks across four blades, minus the quiet mistakes they invite —
an app role whose value doesn't match the code, a redirect URI on the wrong platform, roles assigned
against the client registration instead of the API's.

Undo it with the two `az ad app delete` lines the script prints when it finishes.

The rest of this document is the same thing by hand: read it if you want to know what the script
did, if your organization requires changes to go through the portal, or if something needs
adjusting afterwards.

---

## 1. Register the API

*Microsoft Entra admin center → App registrations → New registration.*

- **Name:** `<your app> API`
- **Supported account types:** *Accounts in this organizational directory only* (single tenant).
- **Redirect URI:** none. An API is not a client.

Then, on the new registration:

**Expose an API →** set the Application ID URI (accept the default `api://<client id>`), then
*Add a scope*:

| Field | Value |
|---|---|
| Scope name | `access_as_user` |
| Who can consent | Admins and users |
| Admin consent display name | Access the API as the signed-in user |

**App roles →** create two. The `Value` column must match `SharedKernel/RoleNames.cs` exactly — it is
a contract with the directory, not a label:

| Display name | Allowed member types | Value | Description |
|---|---|---|---|
| Member | Users/Groups | `Member` | Can use the application |
| Administrator | Users/Groups | `Admin` | Can also see every user |

Record the **Directory (tenant) ID** and the API's **Application (client) ID**.

## 2. Register the SPA

*App registrations → New registration.*

- **Name:** `<your app> SPA`
- **Supported account types:** single tenant.
- **Redirect URI:** platform **Single-page application**, `http://localhost:4200` for local work.
  Add the real origin before deploying.

Then **API permissions → Add a permission → My APIs →** pick the API registration → *Delegated
permissions* → `access_as_user` → **Grant admin consent**.

Record the SPA's **Application (client) ID**.

> Two registrations rather than one: a single registration acting as both client and resource makes
> the `aud` claim ambiguous, and audience validation is the first of the four checks the API
> performs.

## 3. Require assignment

*Enterprise applications → `<your app> API` → Properties →* set **Assignment required?** to **Yes**.

This is the "authorized staff only" switch. Without it, anyone in the tenant can obtain a token for
the API; with it, only principals holding an app role assignment can. It costs nothing and it is
owned by whoever administers the tenant, not by the application.

## 4. Assign the roles

*Enterprise applications → `<your app> API` → Users and groups → Add user/group.*

**Prefer assigning a group over individuals.** IT keeps governing membership through the
joiner/mover/leaver process it already runs, and the application still receives a clean `roles`
claim — no `groups` claim, no overage risk.

Two caveats, both worth confirming before promising this to a customer:

- Group-based assignment to an enterprise application **requires Entra ID P1 or P2**.
- **Nested groups are not supported.** The assigned group must contain the users directly.

Without P1/P2, assign users one at a time here. Everything else works identically.

## 5. Configure the application

Three values, all on the API side:

```powershell
cd src/backend/Web.Api
dotnet user-secrets set "AzureAd:TenantId"    "<directory (tenant) id>"
dotnet user-secrets set "AzureAd:ClientId"    "<API application (client) id>"
dotnet user-secrets set "AzureAd:SpaClientId" "<SPA application (client) id>"
```

Setting `ClientId` is what switches the API from the development scheme to Entra ID. Nothing else
needs changing.

**User secrets rather than `appsettings.json`** because these values belong to one directory, and
`appsettings.json` ships with the template. Put them there and everyone who clones the repository
inherits your tenant — their app then authenticates against a directory they don't belong to, with
no obvious explanation. Environment variables work equally well in a deployed environment.

**The Angular app needs no configuration at all.** It reads `GET /auth-config` from the API at
startup and configures MSAL from the response, so no tenant-specific value exists in the frontend
source to get committed by accident. That endpoint is anonymous by design — it is the step that
happens before anyone can hold a token — and returns only public identifiers that appear in the
browser's address bar during any sign-in redirect.

**Delete the `DevelopmentAuthentication` section from any non-Development configuration.** The
application refuses to start if it finds one — that section bypasses authentication, and a bypass
that ships is not a bug you get to discover slowly.

## 6. Verify

1. Open the app. You should be redirected to Microsoft, sign in, and land back on `/todos`.
2. `GET /users/me` returns a profile — meaning the first request provisioned your local record.
3. Sign in as somebody holding only `Member`: the **Users** item disappears from the navigation and
   `GET /users` returns 403.
4. Remove your app role assignment entirely and reload: every endpoint returns 403, not 200. If it
   returns 200, assignment is not being enforced — recheck step 3.

---

## Troubleshooting

| Symptom | Cause |
|---|---|
| 401 on every call, token looks valid | `aud` mismatch. The SPA must request `api://<api client id>/access_as_user`, not the SPA's own client id or a Graph scope. |
| 403 on every call, sign-in works | No app role in the token. Check the assignment (step 4) and that the role `Value` matches `RoleNames.cs`. |
| `AADSTS50011` redirect URI mismatch | The SPA's redirect URI must be registered under the **Single-page application** platform, not Web. |
| Roles claim missing entirely | Assignment was made on the *SPA* registration instead of the *API* one. Roles must be assigned on the registration whose token the API validates. |
| Startup throws about `DevelopmentAuthentication` | That section is present outside Development. Remove it; see step 5. |
| App never redirects to Microsoft, signs you in as "Dev User" | The API reports `enabled: false` from `/auth-config`, i.e. one of the three user secrets is missing. Check with `dotnet user-secrets list` and `curl http://localhost:5000/auth-config`. |

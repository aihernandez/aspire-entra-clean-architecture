# Entra ID — the identity model for this template

**Status:** implemented and verified against a live tenant (interactive sign-in, 2026-09-05).
**Supersedes:** the earlier version of this file, which planned an *additive* Entra ID scheme
alongside ASP.NET Core Identity. That plan is void — see [Decision 1](#d1--replacement-not-addition).

This document exists so the identity model is a set of **decisions with reasons and sources**,
not an accumulation of code someone has to reverse-engineer later. Every decision below states
what Microsoft actually recommends and links the page it comes from, because the expensive
mistakes in Entra integrations are the ones that look obviously correct until a second tenant,
a service principal, or an employee's surname change proves otherwise.

Scope: an **internal, enterprise line-of-business application**. All humans who use it already
exist in the organization's Entra ID tenant. Nobody self-registers. There is no anonymous
audience.

---

## The canonical model

Microsoft does not present a menu here; for a SPA plus a protected API it describes one shape:

1. **SPA** authenticates with MSAL.js using Authorization Code + PKCE and obtains an access token.
2. **Protected web API** validates that token. The division of labour is explicit:
   *"Access tokens are only validated in the web APIs for which they were acquired by a client.
   The client shouldn't validate access tokens."* The frontend transports the token; it never
   makes a trust decision from it.
3. **App Roles**, declared in the API's own app registration and assigned by a tenant
   administrator, arrive in the `roles` claim and drive authorization.
4. **`oid` + `tid`** is the key under which the application stores anything about a user.

Points 1 and 2 are architecture the template already had in shape (a SPA calling a bearer-token
API). Points 3 and 4 are where all the substance is, and where the previous Identity-based design
was doing something Microsoft explicitly ranks last.

---

## Decisions

### D1 — Replacement, not addition

ASP.NET Core Identity is removed entirely. Entra ID becomes the only authentication authority:
no local passwords, no local password hashes, no local credential lifecycle, no local role store.

The superseded plan argued for keeping Identity as a second scheme, on the reasoning that real
organizations have contractors and external auditors who predate an Entra rollout. That reasoning
is sound in general and wrong for this template's stated scope. An internal LOB app for an
organization that already runs Entra ID has an answer for outsiders that costs no code: the
tenant administrator invites them as **guest (B2B) users**. They then have `oid` and `tid` like
everyone else. Building a parallel password system to serve a case the directory already covers
means owning credential storage, lockout, reset flows and their breach surface forever.

**Consequence to accept honestly:** the self-service account lifecycle — 7 endpoints, their
handlers, validators, tests and Angular pages — is deleted, not archived in place. A backup of
the pre-migration tree exists outside the repository (see the migration commit message). If an
Identity-flavored sibling template is ever wanted, it starts from that backup.

### D2 — The identity key is the pair (`oid`, `tid`), never `oid` alone

From [claims-validation](https://learn.microsoft.com/en-us/entra/identity-platform/claims-validation):

> *"use the immutable claim values `tid` and `oid` as a combined key for application data and
> determining whether a user should be granted access."*

`oid` is an immutable GUID identifying the user, consistent across every application **within one
tenant**. It is not globally unique: the same human in two tenants has two different `oid` values.
Storing `oid` alone works until the day a second tenant appears, and then fails as a silent
collision rather than a loud error — the worst failure shape there is.

So the local key is the **unique pair (`EntraObjectId`, `EntraTenantId`)**, adopted now even
though the template ships single-tenant. It is one extra column today and a data migration
avoided later.

**Why not `sub`:** `sub` is pair-wise — its value is unique to the combination of (application,
tenant, user). Two applications receive different `sub` values for the same person, and recreating
an app registration changes it. Any stored `sub` is a foreign key to something that can change
underneath it.

**Why never email, UPN or username** — this one is a direct prohibition, not a preference:

> *"**Never use claims like `email`, `preferred_username` or `unique_name`** to store or determine
> whether the user in an access token should have access to data. These claims aren't unique and
> can be controllable by tenant administrators or sometimes users, which make them unsuitable for
> authorization decisions. They're only usable for display purposes. **Also don't use the `upn`
> claim for authorization.**"*

This rules out the tempting shortcut of matching an incoming Entra user to an existing local row
by email address. Email is reassignable — an address belonging to someone who left can later be
handed to a new hire, and matching on it would silently grant one person another's data.

### D3 — There is a local `User` table, and it is not an identity store

The application keeps a slim `User` entity keyed as in D2, holding:

- a local `Guid Id` primary key (ours, stable, referenced by foreign keys),
- `EntraObjectId` + `EntraTenantId` (unique together) — the link to the directory,
- a **cached copy** of display name and email, refreshed from token claims on every request that
  carries different values,
- application-owned data (preferences, `IsActive`, `DeactivatedAt`, timestamps).

It holds **no credentials and no roles**.

Microsoft does not mandate a local table — it mandates the *key*. The table is our choice, and the
reason is that the alternative (identity purely in the token, `TodoItem.UserId` storing a raw
`oid`) makes ordinary product requirements expensive: rendering "Assigned to: Ana García" requires
a Microsoft Graph call, listing the app's users requires Graph, every report groups by GUID, and
referential integrity disappears because no foreign key can point at a directory.

**The rule that keeps this honest:** authorization never reads this table. If `PermissionProvider`
ever issues a `SELECT` against it, the local role store has been reintroduced through the back door
and D4 is dead. The table is a display cache plus application data — nothing more.

### D4 — App Roles. Not security groups, not a custom store

> *"In general, **app roles are the recommended solution**. App roles provide the simplest
> programming model and are purpose made for RBAC implementations."*
> — [custom-rbac-for-developers](https://learn.microsoft.com/en-us/entra/identity-platform/custom-rbac-for-developers)

Microsoft's own comparison of the three options:

| | App Roles | Entra groups | Custom data store |
|---|---|---|---|
| Programming model | **Simplest** | More complex | **Most complex** |
| Role values stable across tenants | Yes | **No** | Depends |
| Delivered in the token | Yes (`roles`) | Yes, unless overage | **No** — fetched at runtime |

**Why not groups**, in Microsoft's words:

> *"an application using groups for authorization **breaks in the next tenant** as both the group
> identifier and name could be different. An application using app roles remains safe."*

The `groups` claim carries **GUIDs, not names**, and those GUIDs differ between the development
tenant and the production tenant — meaning per-environment configuration for something that should
be a compile-time constant. Worse, there is a hard **overage limit**: above **200 groups in a JWT**
(150 in SAML, 6 under implicit flow) Entra stops emitting the claim entirely and substitutes a
`_claim_names` / `_claim_sources` pointer to a Graph endpoint the API must then call. This fails
only for users in many groups — executives and IT staff — which is to say it reaches production.

**Where this leaves the template today:** the current design (roles in local SQL, resolved at
runtime by `PermissionProvider` through `UserManager`) is exactly the third column, the one
Microsoft rates most complex and about which it notes *"Developers are responsible for properly
securing the custom data store."* Moving to App Roles is not only what was asked for; it is
leaving the option Microsoft advises against.

**Recommended assignment practice:** assign the App Role **to a security group**, not to
individuals. IT keeps governing membership with the joiner/mover/leaver processes it already runs,
and the application still receives a clean `roles` claim with no `groups` claim and no overage
risk. **Caveat with a licensing cost:** group-based assignment to an enterprise application
[requires Entra ID P1 or P2](https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/assign-user-or-group-access-portal),
and **nested groups are not supported** — the assigned group must contain the users directly.
Without P1/P2, assignment is per-user in the Enterprise Application. This is a question for the
customer's IT department, not a technical choice we can make here.

### D5 — Coarse roles in the cloud, fine-grained permissions in code

App Roles stay few and coarse (`Admin`, and the implicit baseline of "assigned to the app").
`PermissionNames` in `SharedKernel` stays as the fine-grained vocabulary, and `PermissionProvider`
becomes a **pure function of the `roles` claim** — no `UserManager`, no database, no `async`.

Modelling every permission as its own App Role was rejected for two reasons: tokens grow with each
one, and every new permission would turn a deploy of ours into a configuration change in the
customer's tenant, performed by an administrator we do not control. Role assignment belongs to the
cloud; the mapping from role to permissions belongs to the code, versioned with the code that
enforces it.

### D6 — No endpoint authorizes on authentication alone

Every endpoint requires a permission via `HasPermissionAttribute`, which requires an App Role.
None is reachable by merely presenting a valid token.

This is not defensive over-engineering; it closes a trap Microsoft calls out by name:

> *"by only checking the tenant ID and the presence of an `oid` claim, your API could
> **inadvertently authorize all service principals in that tenant** in addition to users."*

"Let anyone from our company in" — implemented as "validate `tid`, require an `oid`" — actually
means "let in every user **and every registered application** in the tenant", including every
automation and script anyone has registered over the years. The App Role requirement is what makes
the intended sentence true.

An architecture test enforces this: any endpoint type lacking a permission requirement fails the
build. `NetArchTest` is already in the solution and this is precisely its job.

### D7 — JIT provisioning now, SCIM as the documented extension point

Two provisioning models exist, and the difference that matters is offboarding.

**JIT (just-in-time)** creates the local row on first authenticated request, from token claims.
It needs no tenant configuration and no extra infrastructure. Its gap: there is no deprovisioning
signal. When somebody leaves, IT disables them in Entra and they immediately stop being able to
authenticate — access is correctly revoked — but their local row lingers forever and the
application never learns of the departure.

**SCIM 2.0** has Entra *push* creates, updates and deletes into an endpoint the application
exposes. Microsoft calls it *"the de facto standard for provisioning"* and credits it with exactly
what JIT lacks: *"instantly removing users' identities from key SaaS apps when they leave the
organization"*, plus group provisioning, orphan-account discovery, brownfield identity matching
and auditable alerts.

**Decision: ship JIT, design for SCIM.** A conformant SCIM endpoint (full schema, filters, PATCH
semantics, pagination) is a project in its own right and does not belong in a template.
JIT already delivers 100% of *access* correctness; the gap is row hygiene. So the model is built
from day one to accept SCIM later without reshaping: `User` carries `IsActive` and
`DeactivatedAt`, and removal is deactivation, never `DELETE`. JIT fills the table; SCIM, when it
arrives, also switches rows off.

**Available immediately at zero cost:** the **"User assignment required"** property on the
Enterprise Application. With it enabled, only a principal holding an App Role assignment can
obtain a token for the API at all. That is the "authorized staff only" switch, owned by IT,
requiring no code.

### D8 — A Development-only authentication scheme

If authentication is 100% Entra, `dotnet run` stops working without an Azure tenant, an app
registration, and an administrator to consent. A template that cannot start on the machine of
someone who just cloned it is not a template.

So a development-only scheme accepts a stub principal with `oid`, `tid` and `roles` configured in
`appsettings.Development.json`, keeping the whole Aspire stack a single command to boot. It engages
only when the host environment is Development **and** `AzureAd:ClientId` is empty; configure a
tenant and Entra ID takes over with no other change. The SPA mirrors this without configuration of
its own: it reads `GET /auth-config` at startup and skips MSAL when the API reports none. Keeping
the tenant out of the frontend source is deliberate — in a template, a checked-in client id is
inherited by everyone who clones it.

This is an authentication bypass, so it is constrained accordingly: registered **only** under
`IsDevelopment()`, and startup **fails loudly** if its configuration section is present in any
other environment. A backdoor with good intentions is still a backdoor; the guard is the entire
point of it being acceptable.

The same mechanism serves `IntegrationTests`, which used to perform real HTTP logins to obtain
tokens — impossible against Entra in CI, where there is no tenant, no interactive sign-in and no
secret to hand out. The handler honours `X-Dev-ObjectId`, `X-Dev-Roles`, `X-Dev-Email` and
`X-Dev-Anonymous` headers, so a test can be a brand-new person, a non-administrator, somebody
holding no app role at all, or anonymous. That also makes authorization failures reproducible by
hand in local development.

### D9 — Email infrastructure stays; two of its three templates go

The `ConfirmEmail` and `PasswordReset` templates exist only to serve credential flows that D1
deletes. They go with them.

`EmailService`, `EmailTemplateEngine`, the MailKit sender, the MailPit integration and the
`Welcome` template are kept. They are working, tested, identity-agnostic infrastructure, and an
internal business application still needs to send mail. `Welcome` gains a natural new trigger:
first JIT provisioning, which is the moment the account genuinely comes into existence.

### D10 — Role administration links to the portal; it is not implemented in-app

With Entra as the authority, the application **cannot** grant a role. Writing a role into local
storage would be precisely the local authorization D4 removes. `SetAdminRole` is therefore deleted
rather than reimplemented.

The alternative — driving assignments through Microsoft Graph — requires the application
permissions `AppRoleAssignment.ReadWrite.All` and `User.Read.All` with tenant admin consent. A
template that demands those out of the box will not pass a security review at any organization
that runs one, and if it does pass, that is worse. The user administration screen instead links to
the Enterprise Application's *Users and groups* blade, and the user list reads the local mirror,
labelled honestly in the UI as showing only people who have signed in at least once.

Graph-backed administration is documented as an opt-in for deployments that want it, not shipped.

---

## Mandatory claim validation

Four validations, none of which the template performs today.

| Validate | Rule | Detail |
|---|---|---|
| **Audience** | `aud` matches this API | v2.0 tokens: the API's client ID (a GUID). v1.0: the App ID URI, e.g. `api://{appId}` |
| **Tenant** | `tid` matches the tenant owning the data | *"Never allow data in one tenant to be accessed from another tenant."* |
| **Subject** | an App Role in `roles` — not merely a present `oid` | see D6 |
| **Actor** | `scp` for delegated tokens | Its **absence** means an app-only or ID token. If app-only callers are ever authorized via `azp`/`appid`, the optional `idtyp` claim must also be validated as `app`, because a delegated token can be held by parties other than the application |

Additionally, reject app-only tokens on user-scoped endpoints. The documented test is that a token
is app-only when `oid` and `sub` carry the same value — such a request has no human behind it and
must not provision a user profile.

**Continuous Access Evaluation (CAE) is not wired up yet** — see the open questions. For an internal
application it is the offboarding story worth having: a disabled or revoked account loses access
within minutes instead of surviving until its token expires. The SPA already handles the
`InteractionRequiredAuthError` that a CAE claims challenge surfaces as, so the remaining work is on
the API side.

---

## Inventory

### Deleted

| | |
|---|---|
| Endpoints | `Register`, `Login`, `RefreshToken`, `ChangePassword`, `ForgotPassword`, `ResetPassword`, `ConfirmEmail`, `SetAdminRole`, `Delete`, `Update` |
| Application | the matching command/query folders under `Application/Users/`, plus `AccessTokensResponse` |
| Infrastructure | `Identity/IdentityService.cs`, `Identity/ApplicationUser.cs`, `Identity/Roles.cs`, `Authentication/TokenProvider.cs` |
| Domain | `Users/RefreshToken.cs`, `Users/UserEmailChangedDomainEvent.cs` |
| Email | `Templates/ConfirmEmail.*`, `Templates/PasswordReset.*` |
| Packages | `Microsoft.AspNetCore.Identity.EntityFrameworkCore` |
| Tests | `Application.UnitTests/Users/` (all four files) |
| Frontend | `auth/` pages (login/register, forgot, reset, confirm), `core/auth-token.service.ts`, `core/auth.guard.ts` |
| Database | seven Identity tables, by dropping the `IdentityDbContext` base |

Because the template holds no production data, `Migrations/` is deleted and regenerated as a
single clean `InitialCreate` rather than carrying a destructive drop migration as a permanent scar.

### Transformed

| | |
|---|---|
| `ApplicationUser` → `Domain/Users/User` | a plain entity: no Identity base class, no credentials, no roles |
| `PermissionProvider` | pure function of the `roles` claim; no DB, no `UserManager`, no `async` |
| `ClaimsPrincipalExtensions` | reads `oid`/`tid` via `Microsoft.Identity.Web`; note ASP.NET Core renames inbound claims by default, so `GetObjectId()` is used rather than a literal `"oid"` lookup |
| `IIdentityService` | reduced to profile lookup and provisioning |
| `GetAll` / `GetById` | read the local mirror, not a directory |
| **new** `GET /users/me` | a necessity, not a convenience: the token carries the directory's `oid` while every id in this application's data is the local `User.Id`, and only the API knows the mapping |
| `Update` (deleted) | name and email are directory-owned. Editing them locally would write to a cache the next request overwrites — a field that silently reverts is worse than one that isn't there |
| `IntegrationTests` | claims injected by a test scheme instead of HTTP login |
| Frontend auth | MSAL redirect; `core/api-authentication-provider.ts` is the single seam where the token is obtained |

### Untouched

`IApplicationDbContext` and the CQRS abstractions, the Todos slice, domain events, HybridCache,
rate limiting, `ProducesProblemResponses()`, OpenTelemetry and the Aspire wiring, the Kiota
generation pipeline, `HasPermissionAttribute` and `PermissionNames` — the permission *mechanism*
survives; only the source of the roles feeding it changes.

---

## Azure configuration

Two app registrations, not one:

- **API** — exposes a scope (`access_as_user`), declares the App Roles, sets *User assignment
  required*.
- **SPA** — a Single-page application platform with a redirect URI, holding delegated permission
  to the API's scope.

One registration acting as both client and resource is possible and muddles audience validation;
the separation is standard and keeps `aud` unambiguous.

Setup steps live in `EntraIdSetup.md`.

---

## Open questions

1. **Does the target tenant have Entra ID P1 or P2?** Without it, assigning App Roles to groups
   is unavailable and assignment is per-user. This changes the administration story described in
   D4 and is a question for the customer's IT department.
2. **Is a Graph-backed administration screen wanted for any specific deployment?** D10 ships the
   portal link; the Graph path stays documented and unbuilt until someone needs it.
3. **Continuous Access Evaluation** — worth enabling for the offboarding guarantee, but it changes
   how the API answers a revoked token (a claims challenge rather than a plain 401), so it wants its
   own pass with a real tenant to test against.

---

## Sources

- [Secure applications and APIs by validating claims](https://learn.microsoft.com/en-us/entra/identity-platform/claims-validation)
- [Custom role-based access control for application developers](https://learn.microsoft.com/en-us/entra/identity-platform/custom-rbac-for-developers)
- [Add app roles and get them from a token](https://learn.microsoft.com/en-us/entra/identity-platform/howto-add-app-roles-in-apps)
- [What is automated app user provisioning in Microsoft Entra ID](https://learn.microsoft.com/en-us/entra/identity/app-provisioning/user-provisioning)
- [Access token claims reference](https://learn.microsoft.com/en-us/entra/identity-platform/access-token-claims-reference)
- [ID token claims reference](https://learn.microsoft.com/en-us/entra/identity-platform/id-token-claims-reference)
- [Manage tokens for Zero Trust](https://learn.microsoft.com/en-us/security/zero-trust/develop/token-management)
- [Assign users and groups to an application](https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/assign-user-or-group-access-portal)

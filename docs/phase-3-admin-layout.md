# Phase 3 — Admin Layout & Navigation (Standalone Task Doc)

> **STATUS: LOCKED.** This doc is self-contained — execute it from this file alone.

## Project context

- The solution is intentionally becoming a small hybrid CMS architecture. The public
  website and admin backend are separate ASP.NET Core applications in one GitHub
  repository (a monorepo), so they can be developed together and deployed as separate
  Render services.
- Intended solution shape:
  ```text
  OMMv2.slnx
  ├── OMM.Public           # public website and member dashboard
  ├── OMM.Admin            # admin-only website/backend
  └── OMM.Shared           # shared entities, DTOs, contracts, reusable services
  ```
- Both applications may initially run on the same machine or host as separate
  processes. The boundary exists so admin requests and future heavy work do not
  directly compete with public web requests.
- Public and admin Identity stores and authentication cookies are separate. Do not
  share `AspNetUsers`, authentication cookies, or Data Protection keys between them.
  An administrator previews the public experience with a separate ordinary member
  account; cross-application single sign-on is intentionally not used.
- Business data may be shared, but database access must be explicit and least
  privileged. The public app must not access the admin Identity store.
- PostgreSQL on Neon is the current database platform. EF Core owns migrations per
  schema/store: `OMM.Public` owns shared business-data migrations and `OMM.Admin`
  owns Admin Identity migrations. Dapper is used for business data access where
  appropriate.
- Stock lookup supports `Database` and `Json` providers through
  `StockLookup:Provider`. The database provider is the default. Results use a
  process-local `IMemoryCache` with `StockLookup:CacheDays` (default 30 days).
- The stock cache is process-local. Admin-triggered refresh and cross-application
  invalidation are later work and must use a deliberate, secured mechanism; an
  `OMM.Admin` memory-cache clear cannot directly clear `OMM.Public` memory.
- Each schema/store has exactly one migration owner. `OMM.Public` must not create
  Admin Identity migrations, and `OMM.Admin` must not create migrations for the
  Public-owned master-data tables.

- `Components/Routes.razor` currently has this `NotAuthorized` handling:
```razor
  <NotAuthorized>
      <RedirectToLogin />
  </NotAuthorized>
```
  **This has a real bug this phase must fix, not work around:** `NotAuthorized`
  fires both for anonymous users *and* for logged-in users who fail a policy check
  (e.g. a normal member hitting `/admin/*`). Right now, both cases redirect to the
  login page — which means a logged-in-but-not-admin user gets bounced back to a
  login screen they're already past, with no explanation. That's confusing, not a
  security hole, but it's exactly the kind of thing that looks like a bug report
  later. Fix it as part of this phase (see Task 1).
- `Components/Account/Pages/AccessDenied.razor` already exists (`/Account/AccessDenied`)
  — a simple "you do not have access" page. Reuse it, don't build a new one.
- `Components/Layout/DashboardLayout.razor` is the existing miner-facing layout —
  sidebar + top bar, injects `IMineService` for profile/notifications. **Do not reuse
  or extend this for admin** — Phase 3 builds a separate, distinct `AdminLayout`.
- `Components/Account/Pages/Manage/_Imports.razor` already demonstrates the pattern
  this phase should copy for gating a whole folder of pages at once:
```razor
  @layout ManageLayout
  @attribute [Microsoft.AspNetCore.Authorization.Authorize]
```
  Every `.razor` page in that folder automatically gets `ManageLayout` and requires
  authentication, with zero per-page boilerplate. This phase does the same thing for
  a new `OMM.Admin/Components/Pages/Admin/` folder, using the `RequireAdminRole` policy from
  Phase 2b instead of a bare `[Authorize]`.
- `wwwroot/app.css` already has `.sidebar-link` / `.sidebar-link.active` styling used
  by `DashboardLayout`. Reusable for `AdminLayout`, but see the visual-distinction
  decision below — don't make the admin area look identical to the miner dashboard.

## Goal

This phase creates the independently hostable `OMM.Admin` application and its admin
shell. It does not create CRUD, grids, reference-data editing, user management,
maintenance controls, cache controls, or reporting.

A `/admin/*` area exists, is visually unmistakable from the miner-facing app, is
  gated to the `RequireAdminRole` policy from Phase 2b, and denies non-admins gracefully
(no raw exception, no confusing redirect loop).

## Locked decisions

1. **New admin project and pages folder: `OMM.Admin/Components/Pages/Admin/`**, with a single
   `_Imports.razor` in it applying `@layout AdminLayout` and
   `@attribute [Microsoft.AspNetCore.Authorization.Authorize(Policy = "RequireAdminRole")]`
   — mirroring the existing `Manage/_Imports.razor` pattern exactly. Every admin page
   goes in this folder and inherits both the layout and the auth gate automatically.

2. **Fix `Routes.razor`'s `NotAuthorized` handling** to distinguish anonymous vs.
   authenticated-but-forbidden (see Task 1). This isn't optional or admin-specific —
   it's a correctness fix for the whole app's authorization flow that this phase
   happens to be the first to actually need.

3. **Visual distinction: dark/red accent, not the miner app's gold/clay palette.**
   The miner-facing app uses `--omm-gold-500`/`--omm-clay-500` for its accents —
   reserved, per existing design-system rule, for metric highlights, not nav chrome.
   `AdminLayout` should look deliberately different so there's zero chance of
   confusing which surface you're in while performing an admin action. Default:
   dark `--omm-ink-900` sidebar (same dark tone as the miner sidebar, for visual
   consistency with the rest of the app) but with a red/danger-colored top border
   or "ADMIN" badge in the header — using the existing `--omm-burden-red` variable
   already defined in `app.css`, not a new color. **This is a low-cost-to-change CSS
   decision** — if you don't like the exact treatment once you see it, it's a quick
   follow-up, not a re-architecture. Flagging it as a default, not asking you to
   review it before starting.

4. **Landing page (`/admin`) shows row counts for all 7 master data tables plus the
   admin user count**, nothing more elaborate for this phase (no charts, no recent-
   activity feed — that's future work if ever wanted). Query
     Admin's read-only `MasterDataDbContext` for the counts; no need for a dedicated
     service layer for something this simple. The context maps shared contracts from
     `OMM.Shared` to Public-owned tables and does not own their migrations.

5. **Sidebar nav for this phase — links exist, most pages are stubs:**
   - **Dashboard** → `/admin` (real — the landing page from decision 4)
   - **KLSE Stocks** → `/admin/klse-stocks` (**stub only** — "Coming in Phase 5" —
     no `AdminDataGrid` exists yet, that's Phase 4)
   - **Institutions** → `/admin/institutions` (**stub only** — Phase 6)
   - **Markets & Sectors** → `/admin/reference-data` (**stub only** — read-only
     browse of seeded data, not scheduled to be built out yet)
   - **Users** → `/admin/users` (**stub only** — not scheduled)

   Every stub page still needs to actually exist and be routable/auth-gated — "stub"
   means placeholder content, not a missing route.

6. **Separate admin login and Identity boundary.** Add the admin app's `/login` route;
   when hosted at the admin service this is reached through the separate
   `OMM.Admin` host (for example `https://admin.example.com/login`). It uses the
   `OMM.Admin` application's separate ASP.NET Identity store and
   authentication system, but accepts only users in the `Admin` or `SuperAdmin` role and redirects
   successful logins to `/admin`. The existing public `/Account/Login` flow and public
   authentication system, but accepts only users in the `Admin` or `SuperAdmin` role and redirects
   successful logins to `/admin`. The existing public `/Account/Login` flow and public
   Identity store remain separate. The hostname/path is not itself a security boundary;
   every admin route remains gated by `RequireAdminRole`.

7. **Future admin-account creation.** Only a `SuperAdmin` may create additional
   admin accounts. The future Users phase will define whether the invitation link and
   temporary password are sent manually or automatically. This phase only provides
   the Users stub and does not create, edit, invite.

## Architecture and deployment notes

- `OMM.Admin` is a separate ASP.NET Core project in the same solution and repository;
  it is not a folder of pages inside `OMM.Public`.
- `OMM.Shared` is the intended home for genuinely shared entities, DTOs, contracts,
  and reusable services. Do not duplicate business rules or EF entities. Do not move
  unrelated code merely to create the project; keep the Phase 3 change focused.
- The public and admin applications use separate Identity stores, cookies, secrets,
  and deployment configuration. Admin users are not public/member users.
- The business-data database can be shared, but connection permissions must be least
  privileged. Public and admin apps must not use the same unrestricted database login.
- Local development uses separate HTTPS processes/ports, for example
  separate HTTPS ports configured by the two projects' launch profiles.
- Production uses separate Render web services from this monorepo. The public service
  retains its existing Render URL; the admin service receives its own `onrender.com`
  URL and may later use `admin.<company-domain>`.
- Configure Render root directories/build filters so public-only changes do not need
  to redeploy the admin service, while changes to shared code are recognized by both.
- Do not put database passwords, Identity secrets, Data Protection keys, or admin
  passwords in GitHub. Use User Secrets locally and the hosting provider's secret
  configuration in production.
- A separate app improves process isolation, but does not protect against outages of
  shared SQL, cache, storage, DNS, or hosting infrastructure. Maintenance controls,
  distributed cache invalidation, and background reports are later work.

## Tasks

0. **Create the solution boundaries.** Confirm the `OMM.Admin` ASP.NET Core project is
   present in `OMMv2.slnx` and add `OMM.Shared` only if shared entities/contracts/services are
   required by both applications. Keep the existing `OMM.Public` project working. Configure
   separate local ports and separate Identity connection/configuration for the two
   applications. Do not copy the public app's pages into `OMM.Admin` and do not create
  a duplicate migration history for the Public-owned master-data tables. Admin's
  own Identity migration history is required.

1. **Fix the admin app's `Routes.razor` `NotAuthorized` template:**
```razor
   <NotAuthorized>
       @if (context.User.Identity?.IsAuthenticated == true)
       {
           <RedirectToAccessDenied />
       }
       else
       {
           <RedirectToLogin />
       }
   </NotAuthorized>
```
   Create `OMM.Admin/Components/Account/Shared/RedirectToAccessDenied.razor`, mirroring the
   existing `RedirectToLogin.razor` exactly, but navigating to
   `Account/AccessDenied` instead of `Account/Login`:
```razor
   @inject NavigationManager NavigationManager

   @code {
       protected override void OnInitialized()
       {
           NavigationManager.NavigateTo("Account/AccessDenied", forceLoad: true);
       }
   }
```

   Add `OMM.Admin/Components/Pages/OmmAdminLogin.razor` at `/login` within the admin
   application (the deployed app is reached through its admin hostname). It must reuse
   the existing Identity login capabilities and email-based login behavior, including
   passkey, two-factor, lockout, and forced-password-change handling. It must reject a
   successfully authenticated user who is not in `Admin` or `SuperAdmin`, and redirect
   an authorized admin to `/admin`. Do not change the existing public login route in
   `OMM.Public`; the admin app uses its separate admin Identity
   store and cookie.

2. **Create `OMM.Admin/Components/Layout/AdminLayout.razor`:**
   - Sidebar with the 5 nav links from "Locked decisions" §5, using `<NavLink>`
     components the same way `DashboardLayout.razor` does (reuse `.sidebar-link` /
     `.sidebar-link.active` CSS classes for structural consistency).
   - Top bar showing the logged-in admin's name/email and a distinct "ADMIN" badge
     (per §3's visual-distinction decision).
   - `@inherits LayoutComponentBase`, renders `@Body` in the main content area, same
     shape as `DashboardLayout.razor`.
   - Does **not** inject `IMineService` — that's member-facing mock data, irrelevant
     here.

3. **Create `OMM.Admin/Components/Pages/Admin/_Imports.razor`:**
```razor
   @layout AdminLayout
   @attribute [Microsoft.AspNetCore.Authorization.Authorize(Policy = "RequireAdminRole")]
```

4. **Create `OMM.Admin/Components/Pages/Admin/Index.razor`** (`@page "/admin"`):
   - Inject `MasterDataDbContext` for business entities and `ApplicationDbContext` for admin user counts.
   - Query and display row counts: `Country`, `Exchange`, `Market`, `Sector`,
     `SubSector`, `Institution`, `Stock`, and total admin users (users with either
     the `Admin` or `SuperAdmin` role — join through `AspNetUserRoles`/`AspNetRoles`,
     don't just count all of `AspNetUsers`, since that would include regular members
     once member accounts exist).
   - Simple card/grid layout is fine — reuse `MetricCard.razor` from
     `Components/Shared/` if it fits cleanly, it's already generic.

5. **Create the 4 stub pages** under `OMM.Admin/Components/Pages/Admin/`:
   - `KlseStocks.razor` (`@page "/admin/klse-stocks"`)
   - `Institutions.razor` (`@page "/admin/institutions"`)
   - `ReferenceData.razor` (`@page "/admin/reference-data"`)
   - `Users.razor` (`@page "/admin/users"`)

   Each just needs a heading and a "Coming in Phase N" message — they exist so the
   nav links resolve and the auth gate is provably working on every route, not just
   `/admin` itself.

## Acceptance criteria (report these back explicitly)

- [x] `dotnet build` succeeds.
- [x] Logging in as `superadmin` and visiting `/admin` shows the layout with correct
      row counts matching what's actually in the database.
- [x] All 4 stub routes (`/admin/klse-stocks`, `/admin/institutions`,
      `/admin/reference-data`, `/admin/users`) load without error when logged in as
      an admin.
- [x] Logging in as a **non-admin** user (if you have a test member account; if not,
      temporarily remove the `Admin`/`SuperAdmin` role from your own test account,
      test, then reassign it) and visiting `/admin` lands on
      `/Account/AccessDenied` — **not** a raw exception, and **not** a redirect back
      to the login page.
- [x] Visiting `/admin` while fully logged out redirects to `/login` (the
      admin login route).
- [x] The admin area is visually distinguishable from the miner dashboard at a      
      glance (confirms the CSS decision in §3 was actually implemented, not skipped).
- [x] `OMM.Admin` is a separate project from `OMM.Public`, uses its own Identity store and
      authentication cookie, and does not share Data Protection keys with `OMM.Public`.
- [x] The public and admin applications can run as separate local processes and the
      solution builds without either project independently applying shared-database
      migrations.

## Explicitly out of scope for this phase

- `AdminDataGrid` component (search/sort/pagination) — Phase 4.
- Any real CRUD for Stock/Institution — Phases 5–6.
- Anything about the reference data (`Country`/`Exchange`/`Market`/`Sector`) being
  editable — it's seeded, not admin-editable, and no phase currently schedules
  building that UI.
- User management UI (creating/editing admin accounts through a page instead of
  seed code) — Phase 7; only a `SuperAdmin` may create additional admin accounts.
- Admin invitation links, temporary-password delivery, maintenance controls, cache
  invalidation, and background reporting — later phases; this phase only documents
  their boundaries and does not implement them.

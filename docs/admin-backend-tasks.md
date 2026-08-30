# Admin Backend & KLSE Master Data — Task Breakdown

> Companion to `docs/market-data-design.md` (schema, locked) and replaces the task
> list embedded in the original `implementation_plan.md`. Each phase below is written
> to be picked up independently — by you, by me in a later session, or by another
> agent (e.g. Codex) — without needing the full conversation history that produced
> `market-data-design.md`. Each phase links back to that doc for schema details
> instead of repeating them.
>
> Work strictly in phase order — each phase's acceptance criteria assume all prior
> phases are done and merged.

## Current architecture and execution status

- The active solution is `OMMv2.slnx` with three projects: `OMM.Public`,
  `OMM.Admin`, and `OMM.Shared`.
- `OMM.Public` and `OMM.Admin` are separate ASP.NET Core applications in one
  repository. They may run on the same host initially and be deployed as separate
  services later.
- PostgreSQL on Neon is the current database platform. The `development` branch is
  the development database; never run migrations against a shared or production
  database during development.
- EF Core remains the migration owner and is retained for ASP.NET Identity and the
  relational model. Dapper is used for the public stock-lookup read path. Do not
  introduce a second competing migration history.
- Public and admin Identity stores, cookies, secrets, and Data Protection keys remain
  separate. Do not implement cross-application single sign-on.
- Stock lookup supports both providers through `StockLookup:Provider`: `Database`
  (default, Dapper/PostgreSQL) or `Json` (the existing
  `wwwroot/data/klse-stocks.json`). The lookup is cached with `IMemoryCache`; the
  default expiration is 30 days and is configured by `StockLookup:CacheDays`.
- The cache is process-local. An admin refresh action cannot clear the public app's
  cache merely by calling its own cache service. Cross-application invalidation must
  be designed and secured when stock CRUD is implemented; it is not part of the
  current admin shell work.
- Phase 1, Phase 2, Phase 2b, and Phase 3 have been completed and merged.
- Phase 4 (`AdminDataGrid`) is the active phase.

---

## Phase 1 — Schema & Migration (Completed)

**Goal:** get `Country`, `Exchange`, `Market`, `Sector`, `SubSector`, `Institution`,
`Stock` into the database, seeded, with the existing `klse-stocks.json` data migrated
in.

---

## Phase 2 / 2b — Public Roles & Admin Identity Bootstrap (Completed)

**Goal:** role-based auth exists in `OMM.Public` and dedicated `admin` schema Identity is bootstrapped in `OMM.Admin` with seeded SuperAdmin/Admin accounts.

---

## Phase 3 — Admin Layout & Navigation (Completed)

**Goal:** a distinct `/admin/*` area in `OMM.Admin`, visually separate from the
miner-facing `DashboardLayout`, gated to the `Admin` / `SuperAdmin` role.

---

## Phase 4 — Reusable `AdminDataGrid` Component (Active)

**Goal:** one generic Blazor component for search + sort + pagination, reused by
every future admin listing page (Stock, Institution, and whatever comes after).
See `docs/phase-4-admin-datagrid.md` for full specification.

**Tasks:**
1. Generic `AdminDataGrid<TItem>` component: column definitions (label, value
   selector, sortable flag), a search box wired to a caller-supplied filter
   predicate, and pagination controls (10/25/50/100 per page, per
   `implementation_plan.md`'s original spec).
2. Keep it presentation-only — sorting/filtering/paging logic lives in the calling
   page's code-behind (or a shared query-building helper), not baked into the
   component, so it's reusable across very different entities.

**Acceptance criteria:**
- Component can render a list of at least two different entity types (even with
  placeholder data) with search, sort, and pagination all functioning, proving it's
  generic and not Stock-specific.

**Depends on:** Phase 3 (needs the admin layout to live inside).

---

## Phase 5 — Stock CRUD Admin Pages

**Goal:** `/admin/klse-stocks` full CRUD, replacing the static JSON as the source of
truth.

**Tasks:**
1. List page (`/admin/klse-stocks`) using `AdminDataGrid`: search across
   `StockCode`/`ShortName_*`/`RicCode`; filter by `Market`, `Sector`, Shariah status;
   sortable columns per `implementation_plan.md`'s original spec (Code, Name, Market,
   Sector, Current Price).
2. Create/Edit form (`/admin/klse-stocks/create`, `/admin/klse-stocks/edit/{id}`):
   - Editable: `StockCode`, `ShortName_*`, `LegalName_*`, `RicCode`, `YahooSymbol`,
     `IsinCode`, `Market` dropdown (cascading from Exchange, which cascades from
     Country), `Sector` dropdown → `SubSector` dropdown (cascading), `ShariahCompliant`
     toggle, `Currency`, `IsActive`.
   - **Read-only, not editable:** every fundamentals field (`CurrentPrice`,
     `MarketCap`, `EPS`, `DPS`, `NTA`, `ROE`, `ROA`, `DebtToEquity`, `CurrentRatio`,
     `PE`, `PB`, `DividendYield`) plus `LastScrapedAt`/`LastCalculatedAt`. Render as
     plain text, not disabled inputs, so it's visually obvious these aren't editable
     here.
3. Soft-delete action (sets `IsDeleted`/`DeletedByUserId`/`DeletedAt`, no hard delete).
4. On create/edit, set `CreatedByUserId`/`ModifiedByUserId` from the current logged-in
   admin's `AspNetUsers.Id`.

**Acceptance criteria:**
- Full CRUD lifecycle works end-to-end against the seeded ~900 stocks.
- Fundamentals fields are visibly read-only in the form.
- Deleting a stock removes it from the list but not from the database.

**Depends on:** Phase 1 (schema), Phase 4 (grid component).

---

## Phase 6 — Institution CRUD Admin Pages

**Goal:** `/admin/institutions` CRUD, since `Institution` is reused outside the admin
area (Mines/FD/savings forms).

**Tasks:**
1. List + create/edit form, same pattern as Phase 5 but simpler (no fundamentals
   section): `InstitutionCode`, `InstitutionName_*`, `InstitutionCategory` dropdown,
   `Country` dropdown, `IsActive`.
2. Soft-delete, same as Phase 5.

**Acceptance criteria:** same shape as Phase 5's, scoped to `Institution`.

**Depends on:** Phase 1, Phase 4.

---

## Phase 7 — Wire Existing Features to the New Tables

**Goal:** replace the remaining static-JSON/free-text stopgaps now that real tables
exist.

**Tasks:**
1. The stock lookup now has a DB-backed implementation querying `Stock`, while
   retaining the JSON provider as an explicit fallback selected by
   `StockLookup:Provider`. Keep the interface unchanged so
   `StockSearchPicker`/`StockAutosuggest` don't need to change. This subtask is
   already implemented early; verify it remains compatible with the completed Stock
   CRUD work.
2. Replace `Mine.Institution` (currently free text) with a dropdown backed by
   `Institution`, in `Mines.razor`'s add-mine modal (and anywhere else institution is
   entered as free text).

**Acceptance criteria:**
- The dividend calculator's stock search still works exactly as before, from the
  user's point of view, and the default provider reads from the database.
- Adding a new Mine offers a real Institution dropdown instead of a free-text box.

**Depends on:** Phase 5 (Stock data must exist and be correct), Phase 6 (Institution
data must exist).

---

## Phase 7b — Admin User Management & Invitation Flow

**Goal:** Allow SuperAdmins to manage admin accounts and invite new team members via a secure email link (see full spec in `docs/phase-7-admin-user-management.md`).

**Tasks:**
1. List view at `/admin/users` using `AdminDataGrid<ApplicationUser>`.
2. "Invite Admin" modal (`AdminInviteModal.razor`) that creates an admin user in `admin.AspNetUsers`, sets `MustChangePassword = true`, and generates a secure password setup token (`GeneratePasswordResetTokenAsync`).
3. Configure `IEmailSender<ApplicationUser>` (Resend/SendGrid) to deliver the activation link (`/Account/ResetPassword?userId=...&code=...`).
4. Force password change flow for first-time invited admins.

**Acceptance criteria:**
- SuperAdmin can invite a new admin by email and assign `Admin` or `SuperAdmin` role.
- Public self-registration remains disabled.
- The invited admin receives an email, sets their password, and logs in securely.

---

## Phase 8 — Future / Not Scheduled Yet

Not part of this round — listed so nobody accidentally starts on these early:

- External fundamentals scraper (populates `CurrentPrice`, `EPS`, `DPS`, `NTA`, etc.
  from a live source; must never touch `PE`/`PB`/`DividendYield`, and must never
  overwrite a manually-edited raw field — see the "manual edit is final" rule in
  `market-data-design.md` §4.7).
- Internal calculation job (recomputes `PE`/`PB`/`DividendYield` from raw fields,
  updates `LastCalculatedAt`).
- Cross-application stock-cache invalidation or a secure admin-triggered refresh
  endpoint. The public cache is process-local, so this must be designed for the
  separate `OMM.Admin` and `OMM.Public` processes rather than assuming a shared
  in-memory cache.
- Superadmin override mode for fundamentals fields (mentioned as "maybe one day" —
  not designed yet).
- Phase 2 market expansion (US: `Country` = US, GICS-based `Sector` seed, Nasdaq/NYSE
  `Exchange` + tier `Market` rows).

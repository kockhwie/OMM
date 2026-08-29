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
- Phase 1 and Phase 2 have been completed and merged. Phase 3 is the next incomplete
  phase. Phase 4 follows Phase 3. The stock lookup portion of Phase 7 was implemented
  early, but Phase 7 remains incomplete until its Institution work and dependencies
  are complete.

---

## Phase 1 — Schema & Migration

**Goal:** get `Country`, `Exchange`, `Market`, `Sector`, `SubSector`, `Institution`,
`Stock` into the database, seeded, with the existing `klse-stocks.json` data migrated
in.

**Tasks:**
1. Create entity classes for all 7 tables per `docs/market-data-design.md` §4,
   including the shared audit columns (§2) on each.
2. Add `DbSet<T>` for each to `ApplicationDbContext`, with EF Core global query filters
   so `IsDeleted == false` is applied automatically unless explicitly overridden.
3. Configure FKs: `Exchange.CountryId`, `Market.ExchangeId`, `SubSector.SectorId`,
   `Stock.MarketId` / `SectorId` / `SubSectorId`, `Institution.CountryId`, and all
   `*ByUserId` columns → `AspNetUsers.Id`.
4. Write an `HasData`/seed migration (or a one-time seed method run at startup in Dev)
   for: `Country` (MY), `Exchange` (BURSA), `Market` (MAIN/ACE/LEAP), `Sector` +
   `SubSector` (13 official Bursa sectors), `Institution` (Maybank, CIMB, Public Bank,
   KWSP, Bursa Malaysia).
5. Write a one-time data migration/seed script that reads
   `wwwroot/data/klse-stocks.json` and inserts a `Stock` row per entry — `StockCode`
   from `code`, `ShortName_EN` from `name`, `MarketId` defaulted to `MAIN`,
   `SectorId`/`SubSectorId` left null (or an "Unclassified" placeholder row — pick one
   and note it in the migration comment).
6. `dotnet ef migrations add AddMasterDataSchema`, verify `dotnet build` and
   `dotnet ef database update` succeed locally.

**Acceptance criteria:**
- Fresh DB migrates cleanly.
- `SELECT COUNT(*) FROM Stock` matches the row count in `klse-stocks.json` (~900).
- Soft-deleting a `Stock` row hides it from a normal query but the row still exists.
- `CreatedByUserId` is nullable in this phase and is `NULL` for every seeded row.
  Do not create a placeholder `AspNetUsers` row.

**Depends on:** nothing (can start immediately).

---

## Phase 2 — Roles & Superadmin Seed

**Goal:** role-based auth exists, and a `superadmin` account exists to log into the
admin backend and to attribute seed data to.

**Tasks:**
1. Add `DisplayName` (string) to `ApplicationUser`.
2. Seed two Identity roles: `Admin`, `User`.
3. Seed a user with username `superadmin`, assigned the `Admin` role. Since
   `RequireConfirmedAccount = true` is already set in `Program.cs`, the seeded user
   must be created with `EmailConfirmed = true` directly (not via the normal
   registration flow) so it can log in immediately.
4. Add an authorization policy (e.g. `RequireAdminRole`) usable via
   `[Authorize(Policy = "RequireAdminRole")]` on admin pages/layout.

**Acceptance criteria:**
- Logging in as `superadmin` succeeds without needing email confirmation.
- A non-admin user hitting an `/admin/*` route gets redirected/denied, not a raw
  exception.

**Depends on:** nothing. Should be done early since Phase 1's seed data references
`superadmin`'s user ID.

---

## Phase 3 — Admin Layout & Navigation

**Goal:** a distinct `/admin/*` area in `OMM.Admin`, visually separate from the
miner-facing `DashboardLayout`, gated to the `Admin` role.

**Tasks:**
1. `AdminLayout.razor` — separate sidebar/nav from the miner-facing app; apply
   `[Authorize(Policy = "RequireAdminRole")]` at the layout or route-group level.
2. Sidebar nav stub with placeholder links for: KLSE Stocks, Institutions, Markets/
   Sectors (read-only browse — these are seeded, not typically hand-edited), Users
   (future).
3. Landing page at `/admin` — simple summary/counts page (row counts per table is
   enough for now).

**Acceptance criteria:**
- `/admin` and `/admin/klse-stocks` (even as a stub page) route correctly and are
  denied to non-admins.
- Visually distinguishable from the public/miner UI so there's no confusion about
  which surface you're in.

**Depends on:** Phase 2 (needs the `Admin` role/policy to gate routes).

---

## Phase 4 — Reusable `AdminDataGrid` Component

**Goal:** one generic Blazor component for search + sort + pagination, reused by
every future admin listing page (Stock, Institution, and whatever comes after).

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

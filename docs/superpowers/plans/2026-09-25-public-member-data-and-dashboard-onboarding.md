# Public Member Data and Dashboard Onboarding Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove all hardcoded public portfolio data, persist authenticated member data in PostgreSQL, and give users a useful zero-data dashboard that guides them through setup.

**Architecture:** `OMM.Public` owns Public Identity and member-data migrations. A separate `MinerProfile` belongs to the authenticated Public user. Mines, MinePositions, Burdens, IncomeRecords, Expenses, Goals, GoalMines, Currencies, and persisted Notifications are explicit EF Core entities in the Public schema. Services obtain the current user server-side, scope every query and mutation by that user, and expose dashboard read models to Blazor components. The dashboard renders zero, partial, and populated states from database results only.

**Tech Stack:** .NET 10, Blazor Web App with Interactive Server components, ASP.NET Core Identity, EF Core 10, Npgsql/PostgreSQL, xUnit integration/shared tests, existing Bootstrap/OMM CSS.

**Spec:** `docs/superpowers/specs/2026-09-25-public-member-data-and-dashboard-onboarding-design.md`

## Global Constraints

- PostgreSQL is the only source of member/profile/financial truth; do not retain demo records or hardcoded financial fallback values.
- `OMM.Public` owns Public member tables and migrations; `OMM.Admin` must not own or migrate them.
- Every member query and mutation is scoped to the authenticated Public Identity user on the server.
- Currency values use a Public Currency master-table foreign key; do not use hardcoded currency strings as persisted source-of-truth values.
- `MinerProfile` is separate from `ApplicationUser`; registration creates a profile but no financial records.
- Use explicit relational entities and relationships; do not serialize the current object graph into one JSON column.
- Use soft deletion for member records and preserve audit timestamps.
- Do not implement automatic notification read-after-X-days in this work; persist read/dismiss state so that later policy can be added safely.
- Do not run `dotnet test` without explicit user permission; builds and read-only EF/migration checks may run normally.
- Do not commit or push automatically.

## Review Focus

- A new account with no records must render a complete dashboard with zero values and onboarding actions, not a blank card or sample data. Test in Task 7.
- A route/form identifier from another user must not expose or mutate that user's record. Test in Task 4.
- A partially populated account must calculate summaries without null or division-by-zero failures. Test in Task 6.
- Multi-currency records must use Currency foreign keys and retain their own currency when the profile default changes. Test in Task 3.
- A notification must remain persisted with read/dismiss state across refreshes and must not be silently auto-read by this release. Test in Task 5.

---

### Task 1: Define and migrate the Public member-data schema

**Files:**
- Create: `OMM.Public/Data/Entities/Currency.cs`
- Create: `OMM.Public/Data/Entities/MinerProfileEntity.cs`
- Create: `OMM.Public/Data/Entities/MineEntity.cs`
- Create: `OMM.Public/Data/Entities/MinePositionEntity.cs`
- Create: `OMM.Public/Data/Entities/BurdenEntity.cs`
- Create: `OMM.Public/Data/Entities/IncomeRecordEntity.cs`
- Create: `OMM.Public/Data/Entities/ExpenseEntity.cs`
- Create: `OMM.Public/Data/Entities/GoalEntity.cs`
- Create: `OMM.Public/Data/Entities/GoalMineEntity.cs`
- Create: `OMM.Public/Data/Entities/NotificationEntity.cs`
- Modify: `OMM.Public/Data/ApplicationDbContext.cs`
- Create: `OMM.Public/Data/Migrations/20260925120000_AddPublicMemberData.cs`
- Create: `OMM.Public/Data/Migrations/20260925120000_AddPublicMemberData.Designer.cs`
- Modify: `OMM.Public/Data/Migrations/ApplicationDbContextModelSnapshot.cs`

**Interfaces:**
- Consumes: existing `ApplicationUser`, `ApplicationDbContext`, master-data conventions, and the approved design decisions.
- Produces: EF entities and `DbSet`s for Currency, MinerProfile, Mine, MinePosition, Burden, IncomeRecord, Expense, Goal, GoalMine, and Notification; all user-owned tables have a required Public Identity owner key and soft-delete/audit columns.

- [x] **Step 1: Write the failing model/schema tests**

Add focused tests that assert the model contains the required entities, owner keys, currency foreign keys, unique `(UserId, Id)` ownership boundaries, `MinePosition -> Mine`, and `GoalMine -> Goal/Mine` relationships. Include a test that the model does not map member data to Admin Identity tables.

- [x] **Step 2: Run the focused tests and verify they fail for missing entities**

Run only the new focused test class after receiving permission to run tests. Expected result before implementation: failures identify missing entity mappings, not test infrastructure errors.

- [x] **Step 3: Implement the relational entities and mappings**

Use explicit types and precision for monetary/percentage fields. Use `CurrencyId` foreign keys on profile and financial records. Use a `PositionType` discriminator on `MinePosition` with nullable fields only where the position type requires them. Use `GoalMine` as a composite relationship. Add query filters for `IsDeleted` and indexes for `(UserId, IsDeleted)`, foreign keys, maturity dates, and notification state.

- [x] **Step 4: Add Currency master seed rows from a defined database source**

Add only the approved initial currency reference rows and codes after confirming the project’s master-data seeding convention. Do not use currency literals in member records. If the required initial currency list is not defined, stop and bring that gap back for discussion instead of inventing it.

- [x] **Step 5: Generate and inspect the migration**

Generate the migration for `OMM.Public` and inspect every `CreateTable`, foreign key, index, precision, query-filter-related column, and seed row. Ensure it does not alter Admin tables or Public master-data ownership.

- [x] **Step 6: Verify the migration against PostgreSQL**

Per the user's direction (the product is not launched and no separate local/development database is available), verify against the configured Neon `neondb`. EF migration history showed `AddPublicMemberData` was already applied. Apply only the pending `AddGoalCurrency` migration using the direct Neon endpoint, then confirm it appears as applied. Do not claim rollback was tested.

- [x] **Step 7: Run the focused tests and build**

Run the approved focused test command and `dotnet build OMMv2.slnx --no-restore`. Expected result: model tests pass and the solution builds without warnings introduced by this task. The generated idempotent SQL script was inspected. The production migration history was also checked after applying `AddGoalCurrency`.

- [ ] **Step 8: Commit**

Commit with `feat(public): add member data schema and currency master data` after review; do not push.

### Task 2: Create profiles safely for existing and newly registered users

**Files:**
- Create: `OMM.Public/Services/IMinerProfileService.cs`
- Create: `OMM.Public/Services/MinerProfileService.cs`
- Modify: `OMM.Public/Program.cs`
- Modify: `OMM.Public/Components/Account/Pages/ExternalLogin.razor`
- Modify: `OMM.Public/Components/Account/Pages/Register.razor`
- Create: `OMM.Public/Data/PublicMemberMigrationService.cs`
- Modify: `OMM.Public/Components/Pages/Settings.razor`
- Test: `OMM.Integration.Tests/PublicProfileTests.cs`

**Interfaces:**
- Consumes: `MinerProfileEntity`, `Currency`, authenticated `ApplicationUser`, and Task 1 migration.
- Produces: `IMinerProfileService.GetCurrentAsync()`, `CreateIfMissingAsync()`, and `UpdateCurrentAsync(MinerProfileUpdate update)`; all methods derive the owner from the current authenticated principal.

- [x] **Step 1: Write failing profile ownership and registration tests**

Cover: new registered user gets one profile, existing Identity users can receive a profile without financial records, profile reads return only the current user, and updating a submitted profile ID cannot target another user.

- [x] **Step 2: Run tests and verify expected failures**

Run the focused profile tests with explicit permission. The implementation was completed before the focused test command was run in this slice; the final focused run passes 4/4. No pre-implementation red test result is claimed.

- [x] **Step 3: Implement the profile service**

Use `IDbContextFactory<ApplicationDbContext>` and the authenticated user accessor. Create profile rows with Identity email and safe incomplete onboarding state; do not fabricate a display name, currency, country, or financial values. Use Currency FK values from the database only.

- [x] **Step 4: Wire registration and external registration**

After successful Identity creation and external-login linking, create the profile in the same logical workflow. Ensure a duplicate retry does not create a second profile. Do not create Mine, Income, Expense, Burden, Goal, or Notification rows.

- [x] **Step 5: Rework Settings as profile setup**

Load and save the current database profile. Use database Currency options. Show incomplete setup state and validation errors; do not use `MockMineService` or hardcoded country/currency defaults as persisted data.

- [x] **Step 6: Verify and build**

Run approved profile tests and `dotnet build OMM.Public/OMM.Public.csproj --no-restore`.

- [ ] **Step 7: Commit**

Commit with `feat(public): persist miner profiles`.

### Task 3: Implement user-scoped mine and position persistence

**Files:**
- Create: `OMM.Public/Services/IMineRepository.cs`
- Create: `OMM.Public/Services/MineRepository.cs`
- Modify: `OMM.Public/Services/IMineService.cs`
- Create: `OMM.Public/Services/DatabaseMineService.cs`
- Modify: `OMM.Public/Components/Pages/Mines.razor`
- Modify: `OMM.Public/Components/Pages/MineDetail.razor`
- Test: `OMM.Integration.Tests/PublicMineOwnershipTests.cs`

**Interfaces:**
- Consumes: Task 1 entities and Task 2 current-user/profile services.
- Produces: database-backed mine CRUD, position CRUD, and mapping to the existing UI models without trusting submitted owner IDs.

- [x] **Step 1: Write failing mine/position persistence and ownership tests**

Cover add/read/update/delete, one-to-many positions, soft-delete exclusion, current-user scoping, foreign-key currency validation, and attempted cross-user route access.

- [x] **Step 2: Run tests and verify expected failures**

Run the focused tests with explicit permission; expected failures must be missing database service behavior.

- [x] **Step 3: Implement queries and mutations**

Every query filters by authenticated `UserId` and `IsDeleted == false`. Every mutation loads the target with the current owner filter before changing it. Save mine and position changes atomically. Map Institution/Stock references through existing master data where the current UI requires them; stop and discuss if a required field has no approved database source.

- [x] **Step 4: Rewire Mines and MineDetail**

Replace all `IMineService` calls that currently reach mock lists with database operations. Preserve existing validation and institution/stock lookup behavior. Render an honest empty list with an Add Mine action.

- [x] **Step 5: Verify and build**

Run approved focused tests and `dotnet build OMM.Public/OMM.Public.csproj --no-restore`.

- [ ] **Step 6: Commit**

Commit with `feat(public): persist user mines and positions`.

### Task 4: Implement burdens, income, expenses, goals, and relationships

**Files:**
- Create: `OMM.Public/Services/MemberRecordService.cs`
- Modify: `OMM.Public/Services/IMineService.cs` or split into focused interfaces without breaking existing consumers
- Modify: `OMM.Public/Components/Pages/Burdens.razor`
- Modify: `OMM.Public/Components/Pages/Income.razor`
- Modify: `OMM.Public/Components/Pages/Goals.razor`
- Create: `OMM.Public/Components/Pages/Expenses.razor` with route `/expenses`
- Modify: `OMM.Public/Components/Layout/DashboardLayout.razor` to expose the Expenses route
- Test: `OMM.Integration.Tests/PublicMemberRecordOwnershipTests.cs`

**Interfaces:**
- Consumes: Tasks 1–3 entities, current-user scope, profile Currency default, and existing validation rules.
- Produces: user-scoped CRUD for Burden, IncomeRecord, Expense, Goal, and GoalMine relationships; frequency normalization for monthly/annual/one-off records.

- [x] **Step 1: Write failing tests for zero, partial, multi-currency, and ownership cases**

Cover a user with no records, only income, only expenses, a burden linked to a mine, a goal linked to multiple mines, one-off/annual/monthly normalization, profile-currency defaults, and foreign-currency records.

- [x] **Step 2: Run tests and verify expected failures**

Run the focused tests with explicit permission; expected failures must identify missing database-backed operations.

- [x] **Step 3: Implement database-backed record operations**

Use Currency foreign keys for every financial record. Never copy a currency code as the source of truth. Store each record’s frequency and calculate monthly equivalents in a tested calculation layer. Preserve soft-delete and owner filtering.

- [x] **Step 4: Rewire member pages**

Replace mock-backed lists and mutations. Add honest empty states and direct Add actions. For expenses, do not reuse burden fields or the old hardcoded `4950` value.

- [x] **Step 5: Verify and build**

Focused ownership/profile tests passed 16/16 and `dotnet build OMM.Public/OMM.Public.csproj --no-restore` passed with 0 warnings and 0 errors. No commit has been created.

- [ ] **Step 6: Commit**

Commit with `feat(public): persist member financial records`.

### Task 5: Persist and generate notifications

**Files:**
- Create: `OMM.Public/Services/INotificationService.cs`
- Create: `OMM.Public/Services/NotificationService.cs`
- Create: `OMM.Public/Services/NotificationGenerationService.cs`
- Modify: `OMM.Public/Components/Pages/Dashboard.razor`
- Test: `OMM.Integration.Tests/PublicNotificationTests.cs`

**Interfaces:**
- Consumes: persisted Mines, MinePositions, Goals, and record freshness from Tasks 1–4.
- Produces: persisted system notification creation, current-user notification reads, and explicit read/dismiss mutations with `ReadAt`/`DismissedAt`.

- [x] **Step 1: Write failing notification persistence tests**

Cover generation from a real maturity date or goal condition, no generation from empty data, duplicate prevention, user ownership, persistence across reload, and read/dismiss state. Assert that no automatic X-day read behavior is active.

- [x] **Step 2: Run tests and verify expected failures**

Run focused tests with explicit permission; expected failures must identify missing persistence/generation behavior.

- [x] **Step 3: Implement notification generation and persistence**

Generate only from database records and use deterministic keys so repeated dashboard loads do not duplicate notifications. Keep future auto-read policy out of this implementation.

- [x] **Step 4: Rewire the dashboard notification section**

Show persisted notifications, a database-backed empty state, and read/dismiss actions. Remove all mock notification content.

- [x] **Step 5: Verify and build**

Focused tests passed 18/18 and `dotnet build OMM.Public/OMM.Public.csproj --no-restore` passed with 0 warnings and 0 errors. No commit has been created.

Run approved focused tests and `dotnet build OMM.Public/OMM.Public.csproj --no-restore`.

- [ ] **Step 6: Commit**

Commit with `feat(public): persist member notifications`.

### Task 6: Build database-backed dashboard summary calculations

**Files:**
- Create: `OMM.Public/Services/DashboardQueryService.cs`
- Create: `OMM.Public/Models/DashboardSnapshot.cs`
- Modify: `OMM.Public/Services/IMineService.cs` or introduce `IDashboardQueryService`
- Modify: `OMM.Public/Components/Pages/Dashboard.razor`
- Modify: `OMM.Public/Components/Shared/WealthBreakdownChart.razor`
- Modify: `OMM.Public/Components/Shared/FreedomGauge.razor`
- Test: `OMM.Shared.Tests/Calculations/DashboardSummaryCalculatorTests.cs`

**Interfaces:**
- Consumes: database-backed member data and frequency/currency rules from Tasks 1–5.
- Produces: a single null-safe dashboard read model containing profile, summary, chart data, top mines, upcoming positions, notifications, goals, and burdens for the current user.

- [ ] **Step 1: Write failing pure calculation tests**

Cover all-zero data, mine-only data, burden-only data, income-only data, annual/monthly/one-off normalization, zero expense denominator, negative growth, and mixed currencies. For mixed currencies, assert that conversion is not performed until an approved FX source/rule exists; the dashboard must not sum incompatible currencies silently.

- [ ] **Step 2: Run tests and verify expected failures**

Run the focused calculation tests with explicit permission; expected failures must be calculation/model failures.

- [ ] **Step 3: Implement the calculation/read-model layer**

Query only current-user records. Return zero collections and zero metrics for empty data. Refuse or clearly separate mixed-currency totals until a database-backed FX conversion design exists; do not use hardcoded exchange rates.

- [ ] **Step 4: Rewire Dashboard.razor to the read model**

Remove direct mock/service aggregation from the component. Keep presentation-only formatting and navigation. Ensure the populated view uses only the returned database-backed read model.

- [ ] **Step 5: Verify and build**

Run approved calculation tests and `dotnet build OMM.Public/OMM.Public.csproj --no-restore`.

- [ ] **Step 6: Commit**

Commit with `feat(public): calculate dashboard from member data`.

### Task 7: Implement the zero-data and partial-data onboarding experience

**Files:**
- Create: `OMM.Public/Components/Shared/DashboardEmptyState.razor`
- Create: `OMM.Public/Components/Shared/DashboardSectionEmptyState.razor`
- Modify: `OMM.Public/Components/Pages/Dashboard.razor`
- Modify: `OMM.Public/Components/Pages/Settings.razor`
- Modify: `OMM.Public/Components/Pages/Mines.razor`
- Modify: `OMM.Public/Components/Pages/Income.razor`
- Modify: `OMM.Public/Components/Pages/Burdens.razor`
- Modify: `OMM.Public/Components/Pages/Goals.razor`
- Modify: `OMM.Public/wwwroot/app.css` only if shared empty-state styling cannot express the design
- Test: `OMM.Integration.Tests/PublicDashboardEmptyStateTests.cs`

**Interfaces:**
- Consumes: Task 6 `DashboardSnapshot` and Task 2 profile onboarding state.
- Produces: zero-data, partial-data, and populated dashboard rendering with direct actions to `/settings`, `/mines`, `/income`, `/burdens`, and `/goals`.

- [ ] **Step 1: Write failing rendering/route tests**

Cover a brand-new profile with no financial records, partial records, configured profile with no mines, and populated records. Assert the onboarding text/actions appear only when appropriate and no mock names, balances, dates, or notification text appear.

- [ ] **Step 2: Run tests and verify expected failures**

Run focused integration tests with explicit permission; expected failures must be missing empty-state markup or incorrect current rendering.

- [ ] **Step 3: Implement the primary onboarding card**

Use the approved copy such as “Let’s start to build your mines” and provide buttons inside the relevant cards. Profile setup should be suggested when incomplete; Mines, Income, Burdens, and Goals should each be actionable.

- [ ] **Step 4: Implement section-level empty states**

Replace blank `foreach` containers with explanation plus one primary action. Preserve zero metric cards, but label them as starting values and avoid claiming financial progress.

- [ ] **Step 5: Verify populated and partial states**

Run the approved rendering tests, manually inspect the zero-data route with a disposable database account, and build the Public project.

- [ ] **Step 6: Commit**

Commit with `feat(public): add dashboard onboarding empty states`.

### Task 8: Remove mock implementation and complete verification

**Files:**
- Delete: `OMM.Public/Services/MockMineService.cs`
- Modify: `OMM.Public/Program.cs`
- Modify: any remaining consumers found by `rg "MockMineService|jason.goh|mine-epf|burden-mortgage|8500|4950" OMM.Public`
- Modify: `OMM.Integration.Tests/PublicWebAppFactory.cs` if database-backed test setup requires a disposable provider
- Modify: `docs/implementation_plan V2.md` or related roadmap docs to mark mock removal/onboarding status accurately

**Interfaces:**
- Consumes: all database-backed services and dashboard states from Tasks 1–7.
- Produces: a Public application with no hardcoded demo financial records and a verified registration-to-zero-dashboard path.

- [ ] **Step 1: Write failing repository scan/registration tests**

Add a test or verification script that fails if `MockMineService` is registered, if known demo identities/IDs remain in production code, or if Public DI resolves the mock service.

- [ ] **Step 2: Run the scan and verify it fails before removal**

Expected result: the scan identifies the current registration and hardcoded mock implementation.

- [ ] **Step 3: Remove the mock implementation and update DI**

Register only the database-backed service(s). Remove unused mock-only code and update all remaining consumers. Do not replace the mock with an empty in-memory fallback.

- [ ] **Step 4: Run the scan and build**

Expected result: no mock registration or known demo financial literals remain; `dotnet build OMMv2.slnx --no-restore` succeeds.

- [ ] **Step 5: Run approved tests and integration checks**

After explicit permission, run the focused tests and `dotnet test OMMv2.slnx`. Verify migration and zero-data behavior against a disposable PostgreSQL database, not a shared/production database.

- [ ] **Step 6: Commit**

Commit with `feat(public): remove mock member data source`.

## Completion Evidence

Before claiming completion, report:

- migration name and disposable PostgreSQL verification result;
- exact Public tables and owner/currency foreign keys created;
- proof that `MockMineService` is no longer registered or referenced;
- zero-data dashboard behavior and each onboarding action;
- partial-data and mixed-currency behavior;
- ownership-isolation test results;
- build result;
- test result only if the user explicitly authorizes running tests;
- exact documentation files updated.

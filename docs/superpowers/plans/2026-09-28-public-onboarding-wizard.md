# Public Onboarding Wizard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the approved Public onboarding design as a resumable, database-authoritative first-login flow that requires currency, keeps optional profile questions skippable, and guides users toward their first Mine without creating financial data automatically.

**Architecture:** Extend the existing Public `MinerProfileEntity` and `MinerProfileService` with persisted onboarding state and user-scoped transitions. Keep registration/profile creation idempotent, add a focused interactive `Onboarding.razor` route with small card/progress components, and make Dashboard consume the persisted state plus existing DB-backed member read models. No new external dependency or in-memory financial fallback is introduced.

**Tech Stack:** .NET 10, Blazor Web App interactive server components, ASP.NET Core Identity, EF Core/Npgsql, existing OMM shared form/CSS primitives, xUnit integration tests.

**Spec:** `docs/superpowers/specs/2026-09-28-public-onboarding-wizard-design.md`

## Global Constraints

- Currency is required before the user can save financial records or financial Mine values.
- Country is recommended but skippable and must not silently determine or overwrite currency.
- Display name is optional and must be explicitly entered by the user.
- The first Mine is never created automatically.
- Every profile/onboarding read and mutation derives the owner from the current authenticated principal server-side.
- Dashboard content is derived from database-backed member data only; do not add demo balances, names, dates, or notifications.
- Mixed-currency totals must not be silently aggregated without an approved FX source and policy.
- Preserve Public soft deletion and audit conventions.
- Do not run `dotnet test` without explicit user permission; do not commit or push automatically.

## Review Focus

- Repeated or concurrent card submissions must not create duplicate profiles or advance a user past an unsatisfied required currency step; owned by the onboarding service transition tests.
- A submitted currency/country ID from another user or an inactive/deleted master row must be rejected; owned by service validation and integration tests.
- Existing Identity users without profiles must be repaired idempotently without fabricated profile values; owned by migration/startup and registration tests.
- A skipped onboarding user must see truthful dashboard actions and must not see demo financial data; owned by dashboard rendering/integration tests.
- Mixed-currency member data must remain explicitly unaggregated; owned by dashboard state tests.

### Task 1: Persist the onboarding contract

**Files:**
- Create: `OMM.Public/Data/Entities/OnboardingState.cs`
- Modify: `OMM.Public/Data/Entities/MinerProfileEntity.cs`
- Modify: `OMM.Public/Data/ApplicationDbContext.MemberData.cs`
- Create: `OMM.Public/Data/Migrations/<timestamp>_AddPublicOnboardingState.cs` and designer/snapshot updates
- Test: `OMM.Integration.Tests/PublicOnboardingTests.cs`

**Interfaces:**
- Produces `OnboardingStatus` (`NotStarted`, `InProgress`, `Skipped`, `Completed`) and `OnboardingStep` values for Welcome, Currency, Country, DisplayName, and FirstMineDecision.
- Produces persisted profile fields for current step, status, started/completed/skipped timestamps, while retaining existing currency/country/display-name columns.

- [ ] Write tests asserting the enum contract, fresh profile defaults, required currency semantics, and persisted state columns.
- [ ] Run the focused tests and confirm the new contract fails before implementation. (Requires explicit permission under `AGENTS.md`.)
- [ ] Add the enums/properties and EF configuration with a single profile primary key, nullable optional fields, and no new identity relationship.
- [ ] Add the PostgreSQL migration and model snapshot update using the repository’s established migration style.
- [ ] Run the focused tests again and confirm the contract passes. (Requires explicit permission.)

### Task 2: Implement idempotent, user-scoped onboarding transitions

**Files:**
- Create: `OMM.Public/Services/IOnboardingService.cs`
- Create: `OMM.Public/Services/OnboardingService.cs`
- Modify: `OMM.Public/Services/IMinerProfileService.cs`
- Modify: `OMM.Public/Services/MinerProfileService.cs`
- Modify: `OMM.Public/Program.cs`
- Test: `OMM.Integration.Tests/PublicOnboardingTests.cs`

**Interfaces:**
- `Task<OnboardingView> GetCurrentAsync(CancellationToken)`
- `Task<OnboardingView> SaveCurrencyAsync(int currencyId, CancellationToken)`
- `Task<OnboardingView> SaveCountryAsync(int? countryId, CancellationToken)`
- `Task<OnboardingView> SaveDisplayNameAsync(string? displayName, CancellationToken)`
- `Task<OnboardingView> CompleteFirstMineDecisionAsync(bool addMineNow, CancellationToken)`
- `Task<OnboardingView> SkipAsync(CancellationToken)`
- `Task<MinerProfileView> CreateForUserAsync(ApplicationUser user, CancellationToken)` remains idempotent and initializes onboarding to `NotStarted`.

- [ ] Write tests for new profile creation, existing-profile repair, resume-after-reload, required currency validation, optional country/name skip, duplicate submissions, and cross-user access.
- [ ] Run focused tests and observe expected failures. (Requires explicit permission.)
- [ ] Implement the service using the authenticated principal for ownership, active DB-backed master data validation, one transaction/save per transition, and monotonic/idempotent transitions.
- [ ] Register `IOnboardingService` and ensure both password and external registration paths continue to call the idempotent profile creator.
- [ ] Run focused tests and confirm they pass. (Requires explicit permission.)

### Task 3: Build the resumable onboarding wizard

**Files:**
- Create: `OMM.Public/Components/Pages/Onboarding.razor`
- Create: `OMM.Public/Components/Shared/OnboardingCard.razor`
- Create: `OMM.Public/Components/Shared/OnboardingProgress.razor`
- Modify: `docs/ui-form-standards.md` only if an existing shared primitive needs a documented additive rule
- Test: `OMM.Integration.Tests/PublicOnboardingTests.cs`

**Interfaces:**
- Route `/onboarding`, `[Authorize]`, uses `IOnboardingService` and existing profile option queries.
- Every successful card action persists before rendering the next card.
- Currency card has no skip action; country/name cards have Skip; first-Mine decision routes to `/mines/new` or `/dashboard` after persistence.

- [ ] Write rendering/HTTP tests for route authorization, card sequence/resumption, currency-required behavior, skip actions, and first-Mine navigation.
- [ ] Run focused tests and confirm expected failures. (Requires explicit permission.)
- [ ] Implement the wizard with existing form classes, accessible labels, validation messages, responsive layout, and no inline one-off form styling.
- [ ] Add links from the wizard to Settings/Dashboard and preserve progress across reload/logout.
- [ ] Run focused tests and confirm they pass. (Requires explicit permission.)

### Task 4: Make registration and dashboard states onboarding-aware

**Files:**
- Modify: `OMM.Public/Components/Pages/Dashboard.razor`
- Modify: `OMM.Public/Components/Shared/DashboardEmptyState.razor`
- Modify: `OMM.Public/Components/Shared/DashboardSectionEmptyState.razor` if needed for direct contextual actions
- Modify: `OMM.Public/Components/Pages/Settings.razor`
- Review/modify surgically: `OMM.Public/Components/Account/Pages/Register.razor`, `OMM.Public/Components/Account/Pages/ExternalLogin.razor`
- Test: `OMM.Integration.Tests/PublicOnboardingTests.cs`, existing Public dashboard tests

- [ ] Write tests for incomplete profile, currency-selected/no-Mine, skipped onboarding, Mine-without-records, partial data, populated data, absence of mocks, and mixed-currency messaging.
- [ ] Run focused tests and confirm expected failures. (Requires explicit permission.)
- [ ] Replace the current profile-completeness heuristic with persisted onboarding state and truthful dashboard actions; keep normal calculated summaries for real data only.
- [ ] Show incomplete setup in Settings without forcing deferred enrichment fields and preserve the existing profile editor behavior.
- [ ] Verify registration and external registration create exactly one profile and never create a Mine/financial/notification row.
- [ ] Run the focused Public test suite and inspect the complete output. (Requires explicit permission.)

### Task 5: Verification and handoff

**Files:**
- Modify: any implementation/test files required by failed verification only

- [ ] Run `git diff --check` and inspect `git status --short`, including untracked migration/test files.
- [ ] Run `dotnet build OMMv2.slnx --no-restore` and capture exit code/output.
- [ ] With explicit permission, run the focused onboarding/integration tests, then the full test command requested by the user.
- [ ] Re-read the design requirements item by item and report any deferred or unverified acceptance criteria; do not claim completion without fresh evidence.

# Public Onboarding Wizard and Guided First-Mine Flow

## Status

Proposed design approved in conversation on 2026-09-28. This document defines the product and implementation boundary; implementation has not started from this document.

## Goal

After email/password or external registration, guide a new Public user through a short, resumable setup journey that collects only information needed to use OMM safely and then directs the user toward creating their first Mine. The experience must replace the confusing empty-dashboard-first impression without fabricating financial data or forcing a large personal-information form.

## Product decisions

- A profile is created automatically after successful registration or external-login account creation.
- Currency is required before the user can save financial records or financial Mine values. It is selected from database-backed Currency master data; no hardcoded or silently inferred default is allowed.
- Country is recommended but skippable. Country selection must not silently determine or overwrite currency.
- Display name is optional and must be explicitly entered by the user. It is not fabricated from an email address.
- Date of birth, gender, avatar, social profile image, annual income, employment, risk profile, and other enrichment fields are deferred to later, contextual flows.
- Annual income is a financial record, not an initial profile field, because users may have multiple income sources and currencies.
- Onboarding can be skipped after the required currency step, exited, resumed after logout, and safely retried without duplicate profile or financial rows.
- The first Mine is never created automatically. The user is offered a direct action to add it.
- Dashboard content is derived from database-backed member data only. Empty and partial states must contain truthful actions, not demo balances, names, dates, or notifications.
- Mixed-currency totals must not be silently aggregated until an approved database-backed FX source and conversion policy exists.

## User flow

### 1. Registration and profile creation

After successful registration or external-login linking, create one `MinerProfile` for the authenticated Public Identity user if one does not already exist. Set onboarding to `NotStarted`. Use trusted Identity email only; do not create a Mine, financial record, goal, burden, expense, or notification.

### 2. Welcome card

Explain that setup takes about one minute, optional questions may be skipped, and currency is needed for financial calculations.

### 3. Currency card

Ask: “What currency do you normally use?” Display database-backed currency choices. This is the only required onboarding answer for the first version. A user cannot save financial records without a valid Currency foreign key.

### 4. Optional country card

Ask where the user lives or manages finances. Allow Skip. Store a database-backed country reference when the schema supports it. Do not infer currency from country.

### 5. Optional display-name card

Ask for a preferred display name. Allow Skip. Store only the submitted value.

### 6. First Mine decision card

Ask: “Do you want to start your first Mine now?” The primary action navigates to `/mines/new`; the secondary action skips and returns to a guided dashboard state. Completion must remain persisted even if the user chooses not to add a Mine immediately.

### 7. Contextual next action

After the first Mine, or after skipping it, present one next action at a time: add income, add a burden, add an expense, or create a goal. These actions remain available from their respective dashboard sections and are not all forced into onboarding.

### 8. Completion and resumption

Persist progress after each successful card. A later login resumes at the first incomplete step or shows the appropriate dashboard state. Completion means the required setup is satisfied and the user has passed the first-Mine decision; it does not mean that every financial section contains data.

## State model

The profile/onboarding model must distinguish at least:

- `NotStarted`
- `InProgress`
- `Skipped`
- `Completed`

Persist the current step and timestamps. The exact enum names may follow existing project conventions, but the behavior must be equivalent. Step transitions must be idempotent and scoped to the authenticated user.

## Boundaries and ownership

- Public owns the profile and onboarding persistence because it is coupled to the Public Identity schema.
- Reusable options, shared DTOs, validation primitives, and cross-cutting infrastructure belong in `OMM.Shared` when another project could need them.
- Every profile/onboarding read and mutation derives the owner from the current authenticated principal server-side.
- Submitted IDs must never be trusted as ownership proof.
- Soft deletion and audit conventions used by Public member data must be preserved.
- Currency and country options must come from authoritative database data.

## Component design

Use a focused wizard rather than a large form:

- `Onboarding.razor` owns step routing and orchestration.
- Small shared components may own the card shell and progress indicator.
- Existing shared form primitives and `docs/ui-form-standards.md` govern labels, spacing, validation, and responsive layout.
- Settings remains the place for later profile editing and must show incomplete setup when onboarding is skipped.
- Dashboard owns zero-data, partial-data, and populated-data presentation, while a query/read-model service owns the data calculation.

## Dashboard states

1. Incomplete profile: show “Complete your setup” and link to onboarding/settings.
2. Currency selected but no Mine: show “Let’s start building your Mines” and a primary Add Mine action.
3. Mine exists but no financial records: show contextual actions for income, burdens, expenses, and goals.
4. Partial data: show available truthful summaries and the next recommended action.
5. Populated data: show the normal database-backed dashboard.

Each section must have a useful empty state and a direct action. Do not show fabricated metrics or claim financial progress for zero values.

## Deferred enrichment

Avatar, social-provider profile images, date of birth, gender, employment, risk profile, and similar fields are separate future work. Before implementing avatars, define consent, provider availability, URL lifetime, fallback behavior, privacy handling, and whether images are copied or referenced. Social claims must not silently become authoritative profile data.

## Implementation sequence

1. Confirm and test the onboarding contract and state model.
2. Add persisted profile/onboarding fields and migration.
3. Implement the user-scoped onboarding service.
4. Wire registration and external login.
5. Implement database-backed currency selection.
6. Build the resumable wizard shell and cards.
7. Add the first-Mine handoff.
8. Rework dashboard zero-data and partial-data states.
9. Add contextual prompts and section actions.
10. Add ownership, state-transition, rendering, and registration integration tests.
11. Verify the full registration-to-dashboard path against a disposable PostgreSQL database.

## Verification requirements

Tests must cover new registration, external registration, existing users without profiles, resumption after logout, required currency, skipped country, duplicate submissions, cross-user access, first-Mine navigation, skipped onboarding, dashboard state transitions, absence of mock financial data, and mixed-currency behavior. Do not run `dotnet test` without explicit user permission. Build and static inspection may be performed according to the repository instructions.

## Out of scope

- Automatic currency defaults.
- Automatic country-to-currency inference.
- Automatic Mine creation.
- Collecting DOB, gender, avatar, social image, annual income, or employment during first-login onboarding.
- FX conversion or mixed-currency aggregation.
- Replacing the approved database-authoritative member-data model with in-memory fallback data.

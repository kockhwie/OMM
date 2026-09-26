# Public Member Data and Dashboard Onboarding Design

## Goal

Replace the public app's hardcoded `MockMineService` portfolio with user-owned PostgreSQL data and make the authenticated dashboard useful for a brand-new account with zero records.

## User outcome

After registration, a user should not see another person's sample portfolio or misleading financial figures. They should see a calm, actionable starting point such as “Let's start to build your mines,” with direct actions to set up their profile, add a mine, record income, add a burden, and create a goal. As records are added, the dashboard should progressively replace the empty sections with real values and summaries.

## Scope

This work covers the public member data boundary and the dashboard's zero-data behavior for:

- miner profile;
- mines and mine sub-records already represented by the current models;
- burdens;
- income records;
- goals;
- dashboard summary calculations;
- notifications derived from member data where the current UI expects them.

The current UI routes and `IMineService` are retained where practical, but the implementation must stop registering `MockMineService` as the public application's member data source.

## Non-goals

- no demo-data toggle or “try a sample portfolio” mode;
- no anonymous-user portfolio persistence;
- no new third-party persistence or UI framework;
- no changes to the Admin master-data schema or migrations;
- no speculative gamification, charts, snapshots, or AI-generated narrative before the underlying member data is real;
- no automatic creation of financial records from a Google profile.

## Design

### Ownership and persistence

Member records are owned by the authenticated Public Identity user. Every member query and mutation must be scoped to the current user ID on the server; the component must not supply an owner ID as a trusted input.

Use the existing `OMM.Public.Data.ApplicationDbContext` and EF Core/Npgsql. Keep public member tables in the `public` PostgreSQL schema. Do not place member portfolio tables in `OMM.Admin` or make Admin own their migrations.

`MinerProfile` is a separate table keyed to the Public Identity user. It stores display name, country, currency, language, and onboarding state. A profile row is created after registration from trusted Identity data, but financial records are never created automatically.

The current Public master-data model does not include a Currency table. Add a Public-owned Currency master table before adding `CurrencyId` foreign keys to member records. Currency selection must use a database foreign key rather than hardcoded currency strings; a profile currency is the default for new records, while each persisted financial record retains its own currency for multi-currency holdings such as US stocks or foreign exchange.

The persistence model should use explicit entities and relationships rather than serializing the existing UI object graph into one JSON column. Existing UI models may remain view models where that reduces churn, but the service maps them to persistence entities and returns user-scoped results.

Use one `MinePosition` child table for fixed-deposit placements, stock lots, property details, gold lots, and other position-level data, with a position type discriminator and nullable type-specific fields. Goals may link to multiple mines through a `GoalMine` join table. Income and expenses support `monthly`, `annual`, and `one-off` frequencies.

### Service boundary

Replace `MockMineService` registration with a database-backed implementation behind `IMineService`, or split the interface into focused services if the current method set makes ownership and transactions unclear. Keep database queries, ownership checks, validation, and summary calculations in services; keep `Dashboard.razor` responsible for presentation and navigation only.

Use `IDbContextFactory<ApplicationDbContext>` for service operations invoked from longer-lived interactive Blazor components. Mutations should validate input, set the authenticated owner from server-side context, and save atomically.

### Zero-data dashboard

The dashboard always renders a valid summary for empty collections:

- mines: RM 0;
- burdens: RM 0;
- net wealth: RM 0;
- passive income: RM 0;
- freedom ratio: 0% with an explanatory label, not a success claim;
- growth: RM 0.

When there are no member records, show a primary onboarding card with the message “Let's start to build your mines” and explicit actions for:

1. Set up profile → `/settings`;
2. Add first mine → `/mines`;
3. Record income → `/income`;
4. Add burden → `/burdens`;
5. Create goal → `/goals`.

Each dashboard section must have an intentional empty state. Empty states should explain what the section is for and provide one direct action. They must not render empty bordered containers, fake totals, or sample names.

### Notifications

Notifications are persisted system-generated records, not hardcoded UI data. They may be generated from maturity dates, goal progress, or stale records. Store read and dismissed state in the database. Automatic policies such as marking a notification read after a configured number of days are a later policy change; this work must not silently implement that behavior.

### Progressive population

As data becomes available, the dashboard should show the existing populated sections using only the signed-in user's records. A partially populated account must work: for example, one mine and no burdens, or income with no mines. Summary calculations must remain null-safe and division-by-zero safe.

### Profile defaults

A newly registered user receives a profile row containing trusted Identity email only. Country, currency, language, and display name remain incomplete or nullable until the user supplies them through `/settings`. The UI must distinguish “profile not configured” from a real user-provided display name and link to `/settings`. It must not use the old hardcoded person or email.

## Acceptance criteria

1. `MockMineService` and its hardcoded financial records are no longer registered or used by the running Public app.
2. A new authenticated user with no member records sees zero values and the onboarding actions above.
3. No dashboard view contains another user's name, email, balances, holdings, burdens, goals, notifications, or sample dates.
4. A user can add a mine, income record, burden, and goal, refresh, and see those records still present.
5. A user cannot read, update, or delete another user's member records by changing a route ID or form value.
6. Dashboard summaries remain correct for zero, partial, and populated datasets.
7. Existing master-data lookups such as institutions and stocks continue to use their existing shared/public data ownership rules.
8. Currency selection is backed by a Public Currency master table and foreign keys; no member record hardcodes a currency as its source of truth.
9. Migrations are safe to apply to a throwaway/local PostgreSQL database before any shared environment is considered.
10. Verification includes focused service tests and the repository's existing integration/build checks; `dotnet test` requires explicit user permission under the repository instructions.

## Planned delivery slices

1. Add the missing Currency master data and define the member persistence model/migration.
2. Implement authenticated, user-scoped profile and portfolio services.
3. Rewire mines, income, burdens, and goals pages to the database service.
4. Persist system-generated notifications and read/dismiss state.
5. Rework dashboard summary and every section for zero/partial data.
6. Add onboarding copy, navigation actions, and profile setup guidance.
7. Verify ownership isolation, migration behavior, and the authenticated zero-data flow.

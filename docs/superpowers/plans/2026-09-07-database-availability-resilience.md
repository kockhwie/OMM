# Database Availability Resilience Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make OMM.Public and OMM.Admin degrade gracefully during PostgreSQL failures, recover automatically when the database returns, and emit at most one outage alert per process-level outage transition.

**Architecture:** Keep one process-local `DatabaseAvailability` singleton per web app, but make its state transitions atomic. HTTP request failures, startup probes, background probes, and Blazor circuit errors all report into that same state object. A hosted monitor uses the existing Npgsql data source directly for a bounded connectivity probe; it does not create an EF `DbContext` merely to run a health check. Only `available -> unavailable` and `unavailable -> available` transitions produce operational logs, while the existing startup alert remains a one-time startup signal. The monitor cannot repair a stale Neon hostname; the runbook must direct an operator to update Render’s `DefaultConnection` and redeploy.

**Tech Stack:** .NET 10, ASP.NET Core Blazor Web App with Interactive Server, the existing EF Core 10 + Npgsql Identity integration, the existing Npgsql data source/driver, hosted services, and the repository’s selected .NET test framework. Do not add a new package for this resilience work.

**Spec:** This plan is the approved design for the database availability review; no separate feature spec exists.

## Global Constraints

- Work only in the authoritative `C:\Users\User\source\repos\OMMv2` checkout.
- Preserve the Public/Admin database boundary: Public owns Public migrations; Admin owns Admin Identity migrations; do not introduce cross-app state or cross-app database ownership.
- Use PostgreSQL/Npgsql and local or throwaway databases for tests/migrations; never use a shared or production database for verification.
- Do not null or discard audit user IDs as part of this work.
- Do not promise automatic repair of connection strings, DNS, Render environment variables, or Neon projects.
- Do not add a user-facing provider switch, new deployment system, migration framework, retry framework, health-check package, or other dependency for this work.
- Treat Npgsql and Dapper as different layers: Npgsql is the PostgreSQL driver; Dapper is an optional mapper on top of it. Do not propose removing Npgsql in favor of Dapper.
- Do not broaden this plan into a full EF-to-Dapper migration. That requires a separate design decision and approval.
- Do not log connection strings, passwords, API keys, or full sensitive exception payloads.
- The availability state is process-local. Multiple Render instances may alert independently; cross-instance deduplication is out of scope.
- Every production behavior change must have a test that failed before the implementation was written.

## Files and Responsibilities

- Modify `OMM.Shared/Database/DatabaseAvailability.cs`: atomic state transitions, Npgsql-based probe registration, background monitor, startup/recheck behavior, availability middleware, and health endpoint integration.
- Modify `OMM.Shared/Infrastructure/DatabaseAlerting.cs`: request-failure notification must be transition-aware and must never turn alert delivery failure into an application failure.
- Add a shared test project, preferably `OMM.Shared.Tests/OMM.Shared.Tests.csproj`, referencing `OMM.Shared` and using the repository’s standard .NET test packages. Do not add Dapper or another database abstraction just for tests.
- Add focused tests under `OMM.Shared.Tests/Database/`: state-transition, middleware, and monitor tests. Do not require a real Neon database.
- Modify `OMM.Public/Program.cs`: register the database availability monitor for the existing Npgsql data source and remove the eager unguarded `NpgsqlDataSource.Create(connectionString)` call. Keep EF Core available for the existing Identity/migration boundary.
- Modify `OMM.Admin/Program.cs`: register the existing connection string through the same safe Npgsql data-source registration and register the database availability monitor against that data source. Keep EF Core for the existing Identity/migration boundary.
- Modify `OMM.Public/Components/App.razor` and `OMM.Admin/Components/App.razor`: add a shared or app-local database-aware Blazor error boundary around interactive routes.
- Add an operational runbook under `docs/`, for example `docs/database-availability-runbook.md`.
- Add or update configuration examples only if the repository already maintains such examples; do not commit live Render or Resend secrets.

## State-machine contract

The implementation must expose behavior equivalent to these contracts, regardless of the final private type names:

```csharp
public sealed class DatabaseAvailability
{
    public bool IsAvailable { get; }
    public bool MarkAvailable();    // true only for unavailable -> available
    public bool MarkUnavailable();  // true only for available -> unavailable
}
```

The initial state is unavailable until the existing startup initialization/check succeeds. `MarkAvailable()` and `MarkUnavailable()` must be atomic and safe when called concurrently by multiple HTTP requests, the hosted monitor, and Blazor circuits. Tests must prove that one transition returns `true` and concurrent duplicate transitions return `false`.

Notification policy:

- Startup initialization failure sends the existing startup alert once for that startup attempt.
- A request or circuit failure sends an outage alert only when `MarkUnavailable()` returns `true`.
- A background probe failure logs the outage transition but does not send a second alert if the state is already unavailable.
- Recovery logs `unavailable -> available`; it does not send an email unless a future explicit recovery-notification requirement is added.
- A later outage after recovery is a new transition and may send one new alert.

## Task 1: Establish the shared test project and state-transition tests

**Files:**
- Create: `OMM.Shared.Tests/OMM.Shared.Tests.csproj`
- Create: `OMM.Shared.Tests/Database/DatabaseAvailabilityTests.cs`
- Modify: solution/project file only if required to include the test project.

- [ ] **Step 1: Create the smallest test project.**

Reference `OMM.Shared`, target `net10.0`, enable nullable and implicit usings, and add only the test SDK/framework packages needed by the repository’s installed toolchain.

- [ ] **Step 2: Write the failing state tests.**

Cover: initial unavailable state; first `MarkAvailable()` returns `true`; repeated `MarkAvailable()` returns `false`; first `MarkUnavailable()` after recovery returns `true`; repeated unavailable calls return `false`; concurrent calls produce exactly one successful transition per direction.

- [ ] **Step 3: Run the tests and verify RED.**

Run `dotnet test OMM.Shared.Tests/OMM.Shared.Tests.csproj --no-restore` after restoring if necessary. The tests must fail because the transition-returning atomic behavior does not yet exist, not because the test project is malformed.

- [ ] **Step 4: Implement only the state machine.**

Use an integer field with `Volatile.Read`/`Interlocked.Exchange` or an equivalent atomic implementation. Keep `IsAvailable` read-only to callers.

- [ ] **Step 5: Run the focused tests and verify GREEN.**

Run the same `dotnet test` command and confirm all state tests pass.

- [ ] **Step 6: Commit the isolated state-machine change.**

Use a focused commit such as `feat: make database availability transitions atomic`.

## Task 2: Add the hosted Npgsql recheck monitor

**Files:**
- Modify: `OMM.Shared/Database/DatabaseAvailability.cs` or create `OMM.Shared/Database/DatabaseAvailabilityMonitor.cs` if that keeps responsibilities clear.
- Modify: `OMM.Shared.Tests/Database/DatabaseAvailabilityMonitorTests.cs`
- Modify: `OMM.Public/Program.cs`
- Modify: `OMM.Admin/Program.cs`

**Interfaces:**

- The monitor receives the existing `NpgsqlDataSource` (or a small interface around it), `DatabaseAvailability`, `ILogger`, and `DatabaseAlertNotifier` only if alerting is needed for the first detected outage. It must not depend on an EF context for connectivity checks.
- Registration must identify the application’s existing Npgsql data source explicitly. Public and Admin may continue using their existing EF `ApplicationDbContext` registrations for Identity and migrations; that is separate from the monitor.

- [ ] **Step 1: Define deterministic monitor seams.**

Do not make tests wait 60 seconds. Extract or inject a small probe delegate/factory around opening an Npgsql connection and executing `SELECT 1`, so tests can control success/failure and use a short interval. Production defaults must be 60 seconds, configurable only through code/config already accepted by the project.

- [ ] **Step 2: Write failing monitor tests.**

Cover: a successful Npgsql probe marks available; a failed connection/open or `SELECT 1` marks unavailable; repeated failures do not notify repeatedly; a later successful probe marks available; a failure during one probe does not terminate the hosted service; cancellation stops the loop cleanly; each connection is disposed after the iteration.

- [ ] **Step 3: Run the monitor tests and verify RED.**

Confirm failures are due to the absent monitor behavior.

- [ ] **Step 4: Implement the monitor.**

Use `PeriodicTimer` or an equivalent cancellation-aware loop. Resolve the existing `NpgsqlDataSource`, open a connection with the cancellation token, execute a parameterless `SELECT 1` with a bounded command timeout, and dispose the connection after the probe. Classify Npgsql/database exceptions and `TimeoutException` as unavailable. Catch and log unexpected monitor exceptions, then continue unless cancellation was requested.

The monitor must not call migrations or seeders. It only checks connectivity. It must not send an email on every failed interval.

- [ ] **Step 5: Register the monitor in both web apps.**

Ensure the registration happens after the corresponding Npgsql data source is registered and does not create a second data source or a second availability singleton. The existing EF context registration remains separate. Confirm the monitor starts with the host and stops with the host.

- [ ] **Step 6: Run focused tests and build both apps.**

Run the monitor tests, then `dotnet build OMM.Public/OMM.Public.csproj --no-restore` and `dotnet build OMM.Admin/OMM.Admin.csproj --no-restore`.

- [ ] **Step 7: Commit the monitor change.**

Use a focused commit such as `feat: recheck database availability in background`.

## Task 3: Make HTTP failure handling transition-aware

**Files:**
- Modify: `OMM.Shared/Infrastructure/DatabaseAlerting.cs`
- Modify: `OMM.Shared.Tests/Database/DatabaseFailureMiddlewareTests.cs`

- [ ] **Step 1: Write failing middleware tests.**

Cover: a database exception calls `MarkUnavailable()`; the first outage request gets HTTP 503 and the friendly availability response; concurrent or subsequent outage requests do not send duplicate notifications; a non-database exception is rethrown; an exception after response headers started is rethrown after state is marked unavailable; notifier failures do not replace the original database failure.

- [ ] **Step 2: Run the tests and verify RED.**

Confirm the current middleware fails because it neither updates availability nor suppresses duplicate notifications.

- [ ] **Step 3: Implement the smallest middleware change.**

Resolve `DatabaseAvailability` from `RequestServices`, call `MarkUnavailable()` before notification, and invoke `DatabaseAlertNotifier.NotifyAsync` only when that call returns `true`. Preserve the existing response-started behavior and exception filter. Do not swallow non-database exceptions.

- [ ] **Step 4: Make the friendly response consistent.**

Use one shared response path for the middleware and availability page where practical. Ensure `/health/db` remains reachable and returns 503 while unavailable. Ensure static assets and non-HTML/API clients are not accidentally given HTML unless that is already the established behavior.

- [ ] **Step 5: Run focused tests and builds.**

Confirm notification count, state, response status, and exception propagation assertions. Then build both applications.

- [ ] **Step 6: Commit the HTTP handling change.**

Use a focused commit such as `fix: deduplicate database outage notifications`.

## Task 4: Guard Npgsql data-source construction in both apps

**Files:**
- Modify: `OMM.Public/Program.cs`
- Modify: `OMM.Admin/Program.cs`
- Add or modify: a small shared data-source factory only if the chosen DI registration cannot be tested directly.
- Add: shared tests for invalid connection-string construction if a testable factory is introduced.

- [ ] **Step 1: Reproduce the construction risk.**

Use a throwaway test or a minimal local host configuration to demonstrate where an invalid `DefaultConnection` fails: during service registration, data-source construction, or first resolution. Do not use a real Render or Neon connection string in tests.

- [ ] **Step 2: Choose the least invasive safe registration.**

Prefer the smallest lazy DI registration or dedicated provider, registered in both Public and Admin, that does not call `NpgsqlDataSource.Create(connectionString)` unguarded during top-level startup. If construction can fail, catch only expected configuration/connection-string exceptions, log a redacted diagnostic, and ensure the app reaches its degraded availability handling instead of emitting a raw process-startup page. Do not introduce another database library to solve this.

Do not return a fake/null data source that will create a later null-reference failure. If the data source is required only by the stock lookup service, make that service’s failure path produce the same database-unavailable signal and 503 behavior. Keep the existing Dapper usage where it already exists; do not expand or remove it as part of this availability task.

- [ ] **Step 3: Add a regression test for invalid configuration.**

Assert that an invalid connection string does not cause an uncaught exception before the application’s availability state and diagnostics are initialized. Assert that valid configuration still produces a usable data source.

- [ ] **Step 4: Run the regression test and both builds.**

Verify the exact failure mode is covered; do not claim Render behavior from a local build alone.

- [ ] **Step 5: Commit the startup-construction change.**

Use a focused commit such as `fix: guard database data-source creation`.

## Task 5: Cover Blazor Server circuit failures

**Files:**
- Create: `OMM.Shared/Components/DatabaseErrorBoundary.razor` and code-behind, or app-local equivalents if shared component references become awkward.
- Modify: `OMM.Public/Components/App.razor`
- Modify: `OMM.Admin/Components/App.razor`
- Add: component/unit tests if the project can test the boundary; otherwise add a documented manual circuit test.

- [ ] **Step 1: Confirm the current limitation.**

Document that `UseDatabaseFailureNotifications` only wraps HTTP middleware and cannot catch exceptions raised later by interactive SignalR circuit events.

- [ ] **Step 2: Write the failing boundary test or harness.**

Create a component that throws a classified database exception during an interactive event/render and assert that the boundary marks availability unavailable, invokes notification only on the state transition, and renders a safe recovery message.

- [ ] **Step 3: Implement a database-aware `ErrorBoundary`.**

Override the boundary error hook, classify nested `DbException`, `NpgsqlException`, and `DbUpdateException`, call `MarkUnavailable()`, and notify only on the transition. Preserve normal ErrorBoundary behavior for non-database exceptions. Do not render exception messages or connection details.

- [ ] **Step 4: Place the boundary around interactive routes in both apps.**

Wrap the route subtree, not only an individual page, so button callbacks and component lifecycle work are covered. Keep the reconnect modal outside or inside deliberately based on the desired recovery behavior. The fallback must tell the user to retry/refresh, because an already-faulted circuit cannot be made healthy merely by flipping the process flag.

- [ ] **Step 5: Verify with a local interactive smoke test.**

Start each app with a local/throwaway database configuration, trigger a controlled database failure in a DB-backed interactive action, and confirm: no raw stack trace in the browser, one alert/log event, availability becomes unavailable, and a refresh can recover after the database is restored.

- [ ] **Step 6: Commit the circuit handling change.**

Use a focused commit such as `feat: handle database failures in blazor circuits`.

## Task 6: Add the operator runbook

**Files:**
- Create: `docs/database-availability-runbook.md`

- [ ] **Step 1: Document the alert meaning.**

Explain that the alert means the app could not reach PostgreSQL; it does not identify whether the cause is Neon DNS, an expired/rotated endpoint, credentials, network access, database saturation, or an application query failure.

- [ ] **Step 2: Document the stale-hostname first response.**

The first check is Neon’s current connection string. Compare it with Render’s `DefaultConnection` environment variable, update the Render variable if stale, then redeploy/restart the service. State explicitly that changing an environment variable without restarting the process does not update the already-built configuration in the running app.

- [ ] **Step 3: Document verification.**

After the change, check `/health/db`, load an HTML route, inspect Render logs for the recovery transition, and verify that the next database-backed request succeeds. Do not treat a local successful connection as proof that Render’s environment is correct.

- [ ] **Step 4: Document alert-noise expectations and limitations.**

One process emits one outage alert per transition. Multiple app instances can still emit separate alerts because availability is intentionally process-local. Resend failures are logged locally and must not crash request handling.

- [ ] **Step 5: Commit the runbook.**

Use a focused commit such as `docs: add database availability incident runbook`.

## Task 7: Full verification and acceptance review

- [ ] Run the complete shared test suite with `dotnet test` and capture the exit code and failure count.
- [ ] Build `OMM.Public` and `OMM.Admin` from the repository root with `dotnet build --no-restore` after the test project is restored.
- [ ] Review the diff for secrets, connection-string values, unrelated refactors, duplicate hosted-service registrations, and accidental migration changes.
- [ ] Verify startup failure, request failure, circuit failure, recovery, and invalid data-source configuration each have an explicit test or documented smoke-test result.
- [ ] Verify `/health/db` is 503 while unavailable and 200 after a successful probe.
- [ ] Verify repeated failed requests and failed monitor probes do not send repeated emails.
- [ ] Verify a recovered process can serve the friendly page first, then return to normal after a new request/refresh.
- [ ] Verify cancellation and shutdown do not produce noisy error logs.
- [ ] Verify no claim is made that the app can automatically repair a stale Neon hostname.
- [ ] Only after all evidence is present, report the exact files changed, test/build commands run, and any environment-specific limitation.

## Explicit non-goals

- No automatic mutation of Render environment variables or Neon settings.
- No cross-instance distributed lock or alert deduplication store.
- No automatic database migrations from the background monitor.
- No DbUp, FluentMigrator, or other migration framework introduction. The choice of migration tooling for non-Identity tables is a separate approved design.
- No broad EF-to-Dapper conversion in this plan. The current pragmatic boundary is EF for Identity/migrations and existing Dapper/Npgsql usage for application data; changing that boundary requires a separate plan.
- No retries around arbitrary user database operations beyond the availability probe.
- No swallowing of non-database exceptions.
- No promise that an existing Blazor circuit can recover in place after its component subtree faults; the user-facing recovery action is refresh/reconnect.

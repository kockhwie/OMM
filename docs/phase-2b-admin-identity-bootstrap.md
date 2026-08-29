# Phase 2b — Admin Identity Bootstrap (Standalone Task Doc)

> **STATUS: LOCKED.** This doc is self-contained — execute it from this file
> alone. It exists because Phase 2
> (`docs/phase-2-roles-superadmin.md`) was built entirely inside `OMM.Public`'s
> Identity store. `OMM.Admin` has its own, deliberately separate Identity store
> (per `docs/phase-3-admin-layout.md`'s locked decision that public and admin never
> share `AspNetUsers`, cookies, or Data Protection keys), and that store currently
> has none of Phase 2's work applied to it. Phase 3 cannot gate `/admin/*` routes to
> `RequireAdminRole` if that policy, and the roles/users behind it, don't exist in
> `OMM.Admin` yet.

## Project context

- `OMM.Admin` is a separate ASP.NET Core project (`OMM.Admin/OMM.Admin.csproj`,
  `net10.0`) with its own `ApplicationDbContext : IdentityDbContext<ApplicationUser>`
  (`OMM.Admin/Data/ApplicationDbContext.cs`).
- **`OMM.Admin.Data.ApplicationUser` is currently a bare `IdentityUser`** — no
  `FirstName`/`LastName`, no `MustChangePassword`. Compare
  `OMM.Public.Data.ApplicationUser`, which already has both from Phase 2.
- `OMM.Admin/Program.cs`'s Identity setup does **not** call
  `.AddRoles<IdentityRole>()` and defines no `AddAuthorization` policies at all.
- `OMM.Admin/Components/Account/Pages/Login.razor` calls
  `PasswordSignInAsync(..., lockoutOnFailure: false)` — lockout is off, same as
  `OMM.Public` was before Phase 2.
- **`OMM.Admin/appsettings.json` still points at SQL Server LocalDB**
  (`OMM.Admin.csproj` references `Microsoft.EntityFrameworkCore.SqlServer`), while
  `OMM.Public` has already moved to PostgreSQL on Neon
  (`Npgsql.EntityFrameworkCore.PostgreSQL`) as part of Phase 1. `docs/phase-3-admin-layout.md`
  states "PostgreSQL on Neon is the current database platform" for the solution as a
  whole — this phase brings `OMM.Admin`'s own Identity store onto the same engine.
  Admin uses the `admin` schema and an Admin-specific connection string; it must not
  share Identity tables, credentials, cookies, or Data Protection keys with Public.
  The MVP may use the same Neon/PostgreSQL instance and development branch as the
  business database, subject to least-privilege database roles. There is no LocalDB
  fallback for this phase.
- No migrations exist yet for `OMM.Admin.Data.ApplicationDbContext` beyond the
  original `CreateIdentitySchema` scaffold migration
  (`OMM.Admin/Data/Migrations/00000000000000_CreateIdentitySchema.cs`).

## Goal

`OMM.Admin` gets its own, fully independent copy of Phase 2's work: roles
(`Admin`, `SuperAdmin`), the `RequireAdminRole`/`RequireSuperAdminRole` policies,
`MustChangePassword` enforcement, two real seeded admin accounts, and account
lockout — all inside `OMM.Admin`'s own Identity store, on its own database. When
this phase is done, Phase 3's `[Authorize(Policy = "RequireAdminRole")]` gate on
`OMM.Admin/Components/Pages/Admin/_Imports.razor` has something real to check
against.

## Locked decisions

1. **Same role model as Phase 2, re-applied to `OMM.Admin`:** roles `Admin` and
   `SuperAdmin`, same permission split table as `docs/phase-2-roles-superadmin.md`
   §3. Do not invent a different role scheme for the admin app — it already governs
   itself, so there is no reason for the two Identity stores to diverge on this
   point.
2. **`OMM.Admin.Data.ApplicationUser` gets the same two fields `OMM.Public` got:**
```csharp
   public class ApplicationUser : IdentityUser
   {
       [MaxLength(50)]
       public string? FirstName { get; set; }

       [MaxLength(50)]
       public string? LastName { get; set; }

       public bool MustChangePassword { get; set; } = false;
   }
```
   No stored `DisplayName` — same reasoning as Phase 2 (compute
   `$"{FirstName} {LastName}"` at render time).
3. **Provider: PostgreSQL on Neon for Admin too.** Add
   `Npgsql.EntityFrameworkCore.PostgreSQL` to
   `OMM.Admin.csproj` (the `Microsoft.EntityFrameworkCore.SqlServer` package can be
   removed once the switch is confirmed working), update
   `OMM.Admin/Program.cs`'s `UseSqlServer(...)` to `UseNpgsql(...)`, and replace the
   LocalDB connection string in `appsettings.json` with a Neon connection string
   (User Secrets locally, per the existing convention — never commit real
   credentials). Use the `admin` schema and Admin-specific connection configuration;
   never commit real credentials.
4. **Two seeded accounts, mirroring Phase 2's exactly** (same usernames/emails,
   independent rows in `OMM.Admin`'s own `AspNetUsers` table — these are **not**
   the same database rows as `OMM.Public`'s seeded accounts, just the same
   people/credentials by convention):
   - **SuperAdmin** — username `superadmin`, email `kockhwie@msn.com`,
     `FirstName`/`LastName` = "Jason" / "Goh", role `SuperAdmin`,
     `MustChangePassword = true`, `EmailConfirmed = true` (bypass confirmation, same
     as Phase 2 — email sending is still a no-op via `IdentityNoOpEmailSender`).
   - **Admin** — username `kockhwie`, email `kockhwie@gmail.com`,
     `FirstName`/`LastName` = "Kock Hwie" / "Goh", role `Admin`,
     `MustChangePassword = true`, `EmailConfirmed = true`.
   - Initial passwords via User Secrets, **new keys distinct from `OMM.Public`'s**
     (e.g. `SeedData:AdminApp:SuperAdminInitialPassword` /
     `SeedData:AdminApp:AdminInitialPassword`) so the two apps' secrets never
     collide in one machine's secret store. Fail loudly and refuse to seed if
     missing — no hardcoded fallback, same rule as Phase 2.
5. **Account lockout: enabled, in `OMM.Admin` only.** Change
   `OMM.Admin/Components/Account/Pages/Login.razor`'s
   `PasswordSignInAsync(..., lockoutOnFailure: false)` to `lockoutOnFailure: true`.
   Identity's default thresholds (5 attempts, 5-minute lockout) unless told
   otherwise — same as Phase 2.
6. **`MustChangePassword` enforcement at admin login**, same minimum-viable scope as
   Phase 2: resolve the user, sign in, redirect flagged users to
   `Account/Manage/ChangePassword`, clear the flag there after a successful change.
   Full navigation blocking beyond the login redirect is out of scope here too.
7. **Authorization policies**, added to `OMM.Admin/Program.cs`:
```csharp
   builder.Services.AddAuthorization(options =>
   {
       options.AddPolicy("RequireAdminRole", policy =>
           policy.RequireRole("Admin", "SuperAdmin"));
       options.AddPolicy("RequireSuperAdminRole", policy =>
           policy.RequireRole("SuperAdmin"));
   });
```
   These are the exact policy names Phase 3 §3 (task 3) already expects to exist —
   do not rename them.

## Tasks

1. Add `.AddRoles<IdentityRole>()` to `OMM.Admin/Program.cs`'s
   `AddIdentityCore<ApplicationUser>(...)` chain.
2. Add `FirstName`/`LastName`/`MustChangePassword` to `OMM.Admin/Data/ApplicationUser.cs`
   per decision 2.
3. Replace SQL Server/LocalDB with Npgsql/Neon according to decision 3, including
   the Admin schema and connection configuration.
4. `dotnet ef migrations add AddAdminRolesAndProfileFields --project OMM.Admin`,
   confirm `dotnet build` succeeds.
5. Add the `AddAuthorization` block from decision 7 to `OMM.Admin/Program.cs`.
6. Add a startup seeding step (scoped service provider, after `app.Build()`, before
   `app.Run()`, check-before-create) that:
   - Creates `Admin`/`SuperAdmin` roles via `RoleManager<IdentityRole>` if missing.
   - Creates the two seeded accounts from decision 4 if missing, reading initial
     passwords from configuration and failing loudly if absent.
7. Change `lockoutOnFailure: false` → `true` in
   `OMM.Admin/Components/Account/Pages/Login.razor` per decision 5.
8. Wire `MustChangePassword` redirect/clear behavior into `OMM.Admin`'s
   `Login.razor` and `ChangePassword.razor` per decision 6 (mirror the equivalent
   `OMM.Public` code paths).
9. Apply the migration to a local/throwaway dev database only — never a shared or
   production database without explicit sign-off, same rule as every prior phase.

## Acceptance criteria (report these back explicitly)

- [ ] `dotnet build` succeeds for the whole solution.
- [ ] Migration applies cleanly to a local/throwaway PostgreSQL dev database using
      Npgsql and the `admin` schema.
- [ ] Both `Admin` and `SuperAdmin` roles exist in `OMM.Admin`'s own `AspNetRoles`.
- [ ] Both seeded accounts exist in `OMM.Admin`'s own `AspNetUsers`, each with the
      correct role assignment.
- [ ] Logging in as `superadmin` (via `OMM.Admin`'s login page) succeeds and
      redirects to the change-password page on first login.
- [ ] After changing the password, `MustChangePassword` is `false` and subsequent
      logins reach the normal return URL.
- [ ] `RequireAdminRole` and `RequireSuperAdminRole` policies exist in
      `OMM.Admin/Program.cs` under those exact names.
- [ ] Failing login 5 times in a row locks the `OMM.Admin` account; the correct
      password is rejected while locked.
- [ ] Confirm `OMM.Public`'s Identity store, seeded accounts, and login flow are
      completely untouched by this phase.
- [ ] State explicitly which User Secrets keys hold the two initial passwords.

## Explicitly out of scope for this phase

- Any of Phase 3's actual layout, navigation, or stub pages — this phase only
  makes Phase 3's auth gate meaningful; it doesn't build what sits behind it.
- The shared master-data read path for the `/admin` landing page's row counts.
  Phase 3 must use shared contracts plus an Admin read-only context, while
  `OMM.Public` remains the sole migration owner for those business tables.
- Real email sending — still a no-op via `IdentityNoOpEmailSender`, same as
  `OMM.Public`.
- 2FA/passkey requirements for admin roles — explicitly skipped, same as Phase 2.
- Any cross-application session sharing or SSO between `OMM.Public` and
  `OMM.Admin` — the two Identity stores and cookies remain fully independent, by
  design.

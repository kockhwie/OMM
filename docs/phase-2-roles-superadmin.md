# Phase 2 — Roles, Admin Users & Account Lockout (Standalone Task Doc)

> **STATUS: LOCKED.** All decisions below are final. This doc is self-contained —
> execute it from this file alone, no need to open the other docs.

## Project context

- ASP.NET Core Blazor Server app, ASP.NET Identity scaffolded via
  `AddIdentityCore<ApplicationUser>()` in `Program.cs`.
- `ApplicationUser : IdentityUser` currently has no extra properties
  (`Data/ApplicationUser.cs`).
- At Phase 2 start, `Program.cs`'s Identity setup did **not** call
  `.AddRoles<IdentityRole>()` — role support was not wired up yet. This phase adds it.
- `UserManager.Options.SignIn.RequireConfirmedAccount = true` is already set — normal
  registration requires email confirmation before login. Seeded admin accounts bypass
  this by setting `EmailConfirmed = true` directly at creation.
- `IEmailSender<ApplicationUser>` (`Components/Account/IdentityNoOpEmailSender.cs`) is
  currently a **no-op** — no email is actually sent for anything (confirmations,
  password resets) anywhere in the app right now. **This phase does not fix that** —
  it's a separate, real piece of work (real SMTP/SendGrid config) for a later phase.
  Until then, password resets can't be delivered by email even though the "forgot
  password" UI flow exists.
- At Phase 2 start, `Login.razor` called
  `SignInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe,
  `lockoutOnFailure: false)` — account lockout was off, for every account, admin and
  member alike. This phase turns it on, app-wide.
- **Phase 1 note:** Phase 1's schema and seed data use `CreatedByUserId = null`
  throughout (no admin user existed yet at that point) — this phase does **not** need
  to match any placeholder ID. The `superadmin` user created here gets its own,
  normally-generated `Id`. From this point forward, anything created through the
  admin UI (Phases 5–6) gets a real, non-null `CreatedByUserId`.

## Goal

Two roles (`Admin`, `SuperAdmin`) exist with a clear permission split; two real admin
accounts are seeded (you, and your data-management colleague); first login forces a
password change; account lockout is enabled app-wide.

## Locked decisions

1. **No stored `DisplayName`.** Every admin user has `FirstName` and `LastName`
   (length 50 each in the PostgreSQL Identity schema) on `ApplicationUser`. Anywhere a name needs to be displayed,
   compute `$"{FirstName} {LastName}"` — don't store a separate display string that
   could drift out of sync.
2. **Roles: `Admin` and `SuperAdmin`.** Not job-title names (`DataEntry`, etc.) — the
   role name reflects privilege level, not day-to-day task, so it doesn't need
   renaming if someone's actual responsibilities shift later.
3. **Permission split:**

   | Action | `Admin` | `SuperAdmin` |
   |---|---|---|
   | Create/edit master data (Stock, Institution, etc.) | ✅ | ✅ |
   | Soft-delete a record (`IsDeleted = true`) | ✅ | ✅ |
   | View job runs / monitoring | ✅ | ✅ |
   | **Purge** a soft-deleted record (permanent DB delete) | ❌ | ✅ |
   | Create/manage admin user accounts | ❌ | ✅ |

   This phase only sets up the **authorization policies** for this split
   (`RequireAdminRole`, `RequireSuperAdminRole`). The actual purge UI/action belongs to
   later CRUD phases (5–6) — it just needs the policy to already exist by then.
4. **`MustChangePassword` flag.** New `bool` column on `ApplicationUser`, with the
   model defaulting to `false` for ordinary accounts. The two Phase 2 seeded admin
   accounts are explicitly created with `true`. On successful login, flagged users are
   redirected to the change-password page; after the password is changed, the flag is
   cleared to `false`. Full navigation blocking is not included.
5. **Two real seeded accounts, not one:**
   - **SuperAdmin** — username `superadmin`, real email `kockhwie@msn.com` (stored as
     a normal `Email` column value on this user's row — not hardcoded anywhere in
     source code, not treated as a special case anywhere in the codebase beyond being
     assigned the `SuperAdmin` role), role `SuperAdmin`.
   - **Admin** — username `kockhwie`, email `kockhwie@gmail.com`, role `Admin`.
   - Both accounts are otherwise completely normal rows in `AspNetUsers` — same table,
     same shape, same rules as any future admin account. Nothing about "being the
     superadmin" is special-cased in code beyond the role assignment itself. This is
     intentional — it's what makes adding a second `SuperAdmin` later a zero-code-change
     operation.
6. **Initial passwords via User Secrets, not hardcoded.** Read both accounts' initial
   passwords from configuration at seed time (`dotnet user-secrets set
   "SeedData:SuperAdminInitialPassword" "..."` and similarly for the Admin account),
   with the seed code **failing loudly and refusing to seed** if the config value is
   missing — never falling back to a hardcoded default. Both accounts have
   `MustChangePassword = true`, so whatever initial password is set here only needs to
   work for exactly one login.
7. **Account lockout: enabled, app-wide.** Change `Login.razor`'s
   `PasswordSignInAsync(..., lockoutOnFailure: false)` to `lockoutOnFailure: true`.
   This affects every login in the app — members and admins alike — not just the new
   admin accounts. Use Identity's defaults unless you have a reason to change them
   (5 failed attempts → locked for 5 minutes) — don't invent custom thresholds without
   flagging it.

## Tasks

1. **Add role support to Identity setup.** In `Program.cs`, change:
   ```csharp
   builder.Services.AddIdentityCore<ApplicationUser>(options => { ... })
       .AddEntityFrameworkStores<ApplicationDbContext>()
       .AddSignInManager()
       .AddDefaultTokenProviders();
   ```
   to add `.AddRoles<IdentityRole>()` into the chain — this is what makes
   `RoleManager<IdentityRole>` injectable at all.

2. **Add fields to `ApplicationUser`:**
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

3. **New migration:**
   `dotnet ef migrations add AddUserProfileFieldsAndRoles --project omm`
   (bundles the new `ApplicationUser` columns; the `AspNetRoles`/`AspNetUserRoles`
   tables already exist from the original Identity scaffold migration, so
   `.AddRoles<IdentityRole>()` itself needs no new tables — just this column
   migration.)

4. **Add authorization policies.** In `Program.cs`'s `AddAuthorization(...)` (add the
   call if it doesn't exist yet):
   ```csharp
   builder.Services.AddAuthorization(options =>
   {
       options.AddPolicy("RequireAdminRole", policy =>
           policy.RequireRole("Admin", "SuperAdmin")); // SuperAdmin can do everything Admin can
       options.AddPolicy("RequireSuperAdminRole", policy =>
           policy.RequireRole("SuperAdmin"));
   });
   ```

5. **Seed roles and the two admin accounts.** Add a startup seeding step (run once,
   check for existence before creating — e.g. in `Program.cs` after `app.Build()`,
   before `app.Run()`, using a scoped service provider):
   - Create `Admin` and `SuperAdmin` roles via `RoleManager<IdentityRole>` if they
     don't already exist.
   - Create the `superadmin` user via `UserManager<ApplicationUser>` if it doesn't
     already exist:
     - `UserName = "superadmin"`, `Email = "kockhwie@msn.com"`
     - `EmailConfirmed = true` (bypass the normal confirmation flow)
     - `FirstName`/`LastName`  = "Jason / Goh"
     - `MustChangePassword = true`
     - `Id` — let Identity generate it normally (no coordination with Phase 1 needed)
     - Password from configuration (`SeedData:SuperAdminInitialPassword`) — fail
       loudly if missing, do not fall back to a hardcoded value
     - Assign to the `SuperAdmin` role
   - Create the second (`Admin`-role) account the same way, with its own
     configuration key for the initial password (e.g.
     `SeedData:AdminInitialPassword`), username `UserName = "kockhwie"`, `Email = "kockhwie@gmail.com"`, 
     `FirstName`/`LastName`  = "Kock Hwie / Goh", `MustChangePassword = true`, assigned to the `Admin` role.

6. **Enforce `MustChangePassword` at login.** The login field accepts an email.
   `Login.razor` resolves it with `UserManager.FindByEmailAsync`, signs in using the
   resolved Identity username, and redirects flagged users to
   `Account/Manage/ChangePassword`. After a successful change,
   `ChangePassword.razor` clears the flag, saves, and refreshes the sign-in session.

7. **Enable account lockout.** In `Login.razor`, change:
   ```csharp
   result = await SignInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);
   ```
   to `lockoutOnFailure: true`. Leave `IdentityOptions.Lockout` defaults untouched
   (5 attempts, 5-minute lockout, enabled for new users) unless told otherwise.

## Execution notes

- The Phase 2 migration was applied to the Neon development branch only
  `omm_phase1_dev_20260827`; no shared or production database was used.
- Initial passwords are configured through User Secrets keys
  `SeedData:SuperAdminInitialPassword` and `SeedData:AdminInitialPassword`; password
  values are intentionally not recorded here.
- Login is email-based while stored Identity usernames remain `superadmin` and
  `kockhwie`.
- The user-confirmed flow passed: initial login, password-change redirect, password
  change, and subsequent login using the new password.
- Full navigation blocking beyond the login redirect was intentionally not implemented.
- Role-policy and account-lockout behavior is implemented; dedicated manual verification
  of those acceptance items remains separate if not already performed.

## Acceptance criteria (report these back explicitly)

- [ ] `dotnet build` succeeds.
- [ ] Migration applies cleanly to a local dev DB.
- [ ] Both `Admin` and `SuperAdmin` roles exist in `AspNetRoles`.
- [ ] Both seeded accounts exist, each with the correct role assignment
      (verify via `UserManager.GetRolesAsync(user)` or `AspNetUserRoles` directly).
- [ ] Logging in as `superadmin` succeeds (no email-confirmation block) and is
      redirected to the change-password page on first login, not the normal landing
      page.
- [ ] After changing the password, `MustChangePassword` is `false` and subsequent
      logins go to the normal return URL.
- [ ] `RequireAdminRole` and `RequireSuperAdminRole` policies both exist and are
      distinguishable — a throwaway test page gated to each is a fine way to confirm
      this without waiting for Phase 3's real admin layout.
- [ ] Deliberately failing a login 5 times in a row locks the account, and the
      correct password is rejected while locked (confirms lockout is actually wired
      up, not just configured).
- [ ] State explicitly where the two initial passwords were configured (which User
      Secrets keys) so Jason can retrieve/reset them.

## Explicitly out of scope for this phase

- Admin layout, sidebar, or any actual `/admin/*` pages — Phase 3.
- Real email sending (SMTP/SendGrid) for confirmations or password resets — separate,
  unscheduled work. "Forgot password" UI exists but won't deliver anything until that
  work happens.
- Any `Stock`/`Institution` CRUD, or the purge UI/action itself — Phases 5–6.
- 2FA/passkey requirements for admin roles — explicitly skipped per Jason's decision.
- Full `MustChangePassword` enforcement beyond the login redirect (e.g. blocking all
  other navigation via middleware until changed) — minimum viable version only, note
  clearly if the fuller version wasn't built.

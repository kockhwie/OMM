# OMM Agent Notes

## Service and infrastructure placement rule

Before writing any service, infrastructure class, or HTTP client, apply this decision rule:

**Ask: "Could any other project in this solution ever need this?"**

- **Yes, or unsure → `OMM.Shared`**. This includes email sending, external API clients (Resend, payment gateways, etc.), background job helpers, caching utilities, config option classes, shared DTOs, and any cross-cutting concern.
- **No, and it is tightly coupled to the Admin identity schema → `OMM.Admin`**. Examples: admin `ApplicationUser` pages, admin-only business logic such as user invitation and audit logging.
- **No, and it is tightly coupled to the Public user schema → `OMM.Public`**. Examples: public `ApplicationUser` pages, portfolio and dashboard features exclusive to the public app.

**Never place something in `OMM.Admin` or `OMM.Public` just because the current feature is for that project.** If in doubt, put it in `OMM.Shared` — it is always cheaper to start shared than to move later.

When registering a shared service, use an extension method in `OMM.Shared` (e.g. `services.AddEmailSender(config)`) and call it from both `OMM.Admin/Program.cs` and `OMM.Public/Program.cs`.

---

## Master-data audit foreign keys

`OMM.Admin` uses admin identities in the `admin` PostgreSQL schema, while master-data tables (`Country`, `Exchange`, `Market`, `Sector`, `SubSector`, `Institution`, and `Stock`) live in the `public` schema. Do not configure `AuditableEntity.CreatedByUserId`, `ModifiedByUserId`, or `DeletedByUserId` as foreign keys to `AspNetUsers` for these shared master-data tables. Store these values as nullable text IDs.

If PostgreSQL reports an error such as:

- `23503: violates foreign key constraint FK_<Table>_AspNetUsers_<AuditField>`
- `42704: constraint FK_<Table>_AspNetUsers_<AuditField> does not exist`

apply both fixes:

1. Map the audit properties as `text` in `OMM.Admin/Data/MasterDataDbContext.cs` and remove the identity relationship from `OMM.Public/Data/ApplicationDbContext.cs`.
2. Add/apply a migration that removes the old audit foreign keys and indexes from the `public` master-data tables. Because database environments can be out of sync, use PostgreSQL `DROP CONSTRAINT IF EXISTS` and `DROP INDEX IF EXISTS` rather than EF `DropForeignKey`/`DropIndex` operations that fail when an object is already absent.

Do not work around this by setting `CreatedByUserId` to null; preserve the admin user ID as audit data.

---

## Scaffolded ASP.NET Identity Components

The Razor files in `OMM.Admin/Components/Account/` and `OMM.Public/Components/Account/` are project-owned scaffolded components (generated at project creation), not compiled assets from `Microsoft.AspNetCore.Identity.UI`. NuGet package updates do not overwrite them.

When working with or modifying these components:

1. **Surgical modifications only**:
   - Avoid rewrites or discarding standard template structures.
   - Keep changes additive (e.g., logging, UI styling, query parameters).
   - Document any deviation from the default template with comments explaining the rationale.

2. **Known template gotchas**:
   - In default `ForgotPassword.razor`, the template includes `!(await UserManager.IsEmailConfirmedAsync(user))`, which silently ignores password reset requests if account emails are not confirmed. When adjusting or bypassing this check, maintain audit logging and ensure security is not compromised.
   - When users reset passwords in `ResetPassword.razor`, confirm their email (`user.EmailConfirmed = true`) if the reset token successfully validates identity.

3. **Consistency across projects**:
   - Both `OMM.Admin` and `OMM.Public` maintain their own `Components/Account/Pages/` trees for their respective identity schemas (`admin` vs `public`). When a bugfix or UX improvement applies to standard account operations (like forgot/reset password or email confirmation), ensure both projects are reviewed and kept consistent.

---

## Configuration & Options Pattern

When defining option/settings classes (e.g., in `OMM.Shared` or project-specific configs):

- **Never hardcode environment-specific values**: Do not hardcode sender emails, domain URLs, API endpoints, or provider keys as default string initializers in options classes.
- **Bind cleanly via `IConfiguration`**: Always use `builder.Services.Configure<TOptions>(configuration.GetSection(...))` or options validation.
- Provide clear keys in `appsettings.json` with empty or placeholder strings so required settings are discoverable.

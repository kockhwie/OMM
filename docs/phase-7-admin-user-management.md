# Phase 7 — Admin User Management & Invitation Flow

> **Document Status:** Final Development Record  
> **Target Audience:** Developers maintaining OMMv2

---

## 1. Phase 7 Objective

Provide a secure, auditable, invitation-based user administration system at `/admin/users` in `OMM.Admin` restricted to `SuperAdmin` (and optionally `Admin` with view permissions). All new admin accounts are provisioned via an internal SuperAdmin invitation workflow.

Phase 7 also completed the remaining wiring from shared master data into the public application: the mine institution selector and database-backed stock lookup.

## 2. Completed Master-Data Wiring

### Institution-backed mine selector

- `OMM.Public/Components/Pages/Mines.razor` uses a database-backed institution dropdown.
- Only active, non-deleted institutions are offered.
- Labels use `InstitutionCode - InstitutionName_EN`.
- The existing `Mine.Institution` string remains unchanged for backward compatibility.
- An empty institution selection remains available.
- A relational `InstitutionId` was intentionally not added to `Mine`.

### Database-backed stock lookup

- `StockLookup:Provider` defaults to `Database`.
- `Json` remains an explicit fallback provider.
- Database results contain active, non-deleted stocks ordered by code.
- Existing `StockAutosuggest`, `StockSearchPicker`, and `IKlseStockLookupService` contracts remain unchanged.
- Lookup results are cached in process and can be refreshed.

---

## 3. Consolidated Architecture & Workflow (summary)

- SuperAdmin uses `/admin/users` to invite new admin accounts (Admin or SuperAdmin).
- Invitation creates an admin schema user with MustChangePassword=true and EmailConfirmed=false, assigns role, generates a password-setup token (GeneratePasswordResetTokenAsync), and emails an activation link using `IEmailSender<ApplicationUser>`.
- Activation link points to `/Account/ResetPassword?email=...&code=...` where user sets a password and completes onboarding.
- Security: Enforce token TTL (24h), prevent self-demotion/lockout, ensure >=1 active SuperAdmin.

---

## 4. Consolidated Feature List (deliverables)

1. Admin users listing page: `Pages/Admin/Users.razor` (uses `AdminDataGrid<ApplicationUser>`)
   - Columns: Username, Email, Role(s), EmailConfirmed, MustChangePassword, LockoutEnd, CreatedAt
   - Row actions: Resend Invite, Force Password Reset, Lock/Unlock, Deactivate/Reactivate
2. Invite Modal component: `Components/Admin/AdminInviteModal.razor`
   - Fields: Email, Username, DisplayName, Role
   - Client validation + server-side uniqueness checks
3. Backend service: `Services/Admin/UserManagementService.cs` (interface `IUserManagementService`)
   - Methods: InviteAsync(InviteDto), ResendInviteAsync(userId), ForcePasswordResetAsync(userId), SetLockoutAsync(userId, lock), DeactivateAsync(userId), ReactivateAsync(userId)
4. Identity wiring: use existing `ApplicationUser` in `OMM.Admin` (admin schema). Confirm `Program.cs` DI and policies (`RequireSuperAdminRole`, `RequireAdminRole`).
5. Email: replace `IdentityNoOpEmailSender` with a real provider implementation behind `IEmailSender<ApplicationUser>` and environment-stored API keys.
6. Auditing & security rules: implement self-demotion guard, SuperAdmin threshold check, and audit logs for invite/edit actions.
7. Tests: unit tests for service methods and integration tests for invite flow (token generation, email send mock, reset password flow).
8. Migrations: if new fields required (e.g., MustChangePassword) ensure EF migrations are added in `OMM.Admin` project (admin schema migrations).

---

## 5. API Shapes & DTOs

- InviteDto
  - Username: string
  - Email: string
  - DisplayName: string
  - Role: string

- UserListDto
  - Id, Username, Email, Roles[], EmailConfirmed, MustChangePassword, LockoutEnd, CreatedAt

- Service responses: standard Result<T> pattern with error codes for duplicate email/username, role missing, policy violations.

---

## 6. File & Component Map (implemented and follow-up targets)

- Pages/Admin/Users.razor — list page; wired to AdminDataGrid<ApplicationUser>
- Components/Admin/AdminInviteModal.razor — invite modal component
- Components/Admin/ConfirmActionModal.razor — reuse for lock/deactivate actions
- Services/Admin/IUserManagementService.cs — contract
- Services/Admin/UserManagementService.cs — implementation using UserManager<ApplicationUser>, RoleManager<IdentityRole>
- Data/Migrations/* — EF migrations for admin schema (if needed)
- Program.cs (OMM.Admin) — register real IEmailSender<ApplicationUser>, ensure policies
- Tests/OMM.Admin.Tests — unit/integration tests for invite flow

---

## 7. Acceptance Criteria

- SuperAdmin can invite a new admin; invitee receives an email with working activation link.
- Invite token expires after 24 hours and cannot be reused after password set.
- SuperAdmin cannot demote or lock their own session; system prevents last SuperAdmin removal.
- Admin listing supports required columns, actions, and server-side paging/search.
- Email provider is configurable via environment variables and tests can mock it.

---

## 8. Implementation Tasks & Estimates (prioritized)

1. (Small 2-4h) Consolidate docs & confirm existing Identity wiring (Program.cs) — update this doc. (docs/phase-7-admin-user-management.md)
2. (Small 3-6h) Implement `AdminInviteModal.razor` UI and client validation; wire to a new endpoint in `UserManagementService.InviteAsync`.
3. (Medium 1-2d) Implement `UserManagementService` backend using `UserManager<ApplicationUser>`, `RoleManager<IdentityRole>`. Generate token and email via `IEmailSender<ApplicationUser>`.
4. (Small 2-4h) Replace `IdentityNoOpEmailSender` registration with a configurable provider wrapper and add EnvVar config in `Program.cs`.
5. (Medium 1-2d) Implement listing page using existing `AdminDataGrid` component, add row actions and modals for Force Reset, Lock/Unlock, Deactivate.
6. (Small 2-4h) Add security guards: prevent self-demotion and SuperAdmin threshold enforcement.
7. (Small 1-3h) Add EF migrations if necessary and run local DB verification.
8. (Medium 1-2d) Add unit tests and a simple integration test for the invite -> reset-password -> login happy path (mock email sender to capture token).
9. (Small 1-2h) Update docs, QA checklist, and create issue tickets/PR checklist.

---

## 9. QA Checklist

- [ ] Invite email received and link decodes to a valid token
- [ ] Token TTL honored (24h)
- [ ] Role assignment persists and affects authorization
- [ ] Cannot remove last SuperAdmin
- [ ] Cannot demote/lock own active session
- [ ] Audit entries created for invite/edit actions

---

## 10. Next Steps

- If you confirm this consolidated plan, I will:
  1. Create scoped tasks/issues for the top 3 priority items.
  2. Scaffold `IUserManagementService` + `AdminInviteModal.razor` skeleton and open an initial PR for review.

## 11. Implementation Notes

- Implemented `IUserManagementService` and `UserManagementService` in `OMM.Admin/Services/Admin`.
- `/admin/users` is an interactive Blazor Server page authorized by `RequireAdminRole` and uses `AdminDataGrid<UserListDto>`.
- SuperAdmin-only mutations should be enforced by the surrounding admin navigation and action authorization before production release; the service enforces account safety invariants.
- The existing `IdentityNoOpEmailSender` remains registered. Configure a real provider before production; the current service returns an error if delivery fails.
- No migration was added because the implementation reuses `FirstName`, `LastName`, `MustChangePassword`, `EmailConfirmed`, and Identity lockout fields already present in the admin schema.
- No audit-log persistence or test project exists in the current solution; these remain explicit follow-up tasks.


---

[End of consolidated Phase 7 plan]

# Phase 7 Handoff — Admin & User Management

> **Status:** Implementation handoff
> **Primary development document:** [`phase-7-admin-user-management.md`](./phase-7-admin-user-management.md)

## Delivered

- Completed public master-data wiring for the mine institution selector and stock lookup.
- Replaced the `/admin/users` placeholder with an interactive Blazor Server page.
- Added `RequireAdminRole` authorization and `AdminDataGrid<UserListDto>` search/listing.
- Added invitation form validation and feedback through `AdminInviteModal.razor`.
- Added `IUserManagementService` and `UserManagementService` for invitations, password resets, role updates, lockout, and reactivation.
- Added protections against self-lockout, self-deactivation, self-demotion, and removal of the last active SuperAdmin.
- Configured Identity token lifetime to 24 hours.
- Added the SuperAdmin-only `/admin/email-test` page for manually testing email delivery with To, Subject, and Content fields.
- Registered the user-management service in `OMM.Admin/Program.cs`.
- Verified the solution builds successfully.

## Important Current State

- `ResendEmailSender` is now registered and uses the Resend HTTPS API.
- The API key is never stored in source control; it is read from `Resend_EmailOnboardingApi`.
- Deactivation currently uses Identity lockout because `ApplicationUser` has no separate soft-delete flag.
- The existing profile fields are used as follows: invitation `DisplayName` is stored in `FirstName`; `LastName` remains available for future profile editing.
- No persistent audit-log store exists yet.
- No dedicated OMM.Admin test project exists in the current solution.

## Next Implementation Work

1. Configure and verify production email delivery.
   - Configure `Resend_EmailOnboardingApi`, `Email__FromAddress`, `Email__FromName`, and `AdminBaseUrl` as Render environment variables.
   - Verify the sending domain through Cloudflare DNS.
   - Keep credentials out of source control.
2. Add persistent audit logging for invite, role, lockout, reset, and reactivation actions.
3. Add SuperAdmin-only authorization around mutation actions, while allowing Admin users read-only access if desired.
4. Add edit/role-management UI and confirmation dialogs.
5. Add unit and integration tests for invitation, token expiry, role changes, and safety guards.
6. Re-run database migrations and the full build after each schema or Identity change.

## Verification Checklist

- [x] OMM.Admin solution build succeeds.
- [ ] Invite creates a user in `admin.AspNetUsers`.
- [ ] Invite assigns the selected role.
- [ ] Invitation email is delivered by a real provider.
- [ ] Reset token expires after 24 hours.
- [ ] Password setup clears `MustChangePassword`.
- [ ] Role changes affect authorization.
- [ ] Last active SuperAdmin cannot be removed.
- [ ] Active user cannot lock or deactivate their own account.
- [ ] Audit records are persisted.

## Handoff Files

Only these two Phase 7 documents should be maintained:

- `docs/phase-7-admin-user-management.md` — final development record and detailed specification.
- `docs/handoff-phase-7.md` — current implementation status and next steps.

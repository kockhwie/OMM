# Phase 6 — Completion Handoff: Institution CRUD Management

> **Document Status:** Complete
> **Phase:** 6 of N
> **Predecessor:** [`handoff-phase-5.md`](./handoff-phase-5.md)
> **Successor:** Phase 7 — Wire Existing Features to the New Tables

## 1. Summary

Phase 6 delivered the complete Institution CRUD lifecycle for the OMM Admin Portal at `/admin/institutions`.

- Replaced the placeholder page with an interactive `AdminDataGrid<Institution>`.
- Added server-side search, filtering, sorting, paging, summary counts, and active-status toggling.
- Added `InstitutionEditModal.razor` for create/edit operations.
- Added `DeleteInstitutionModal.razor` for soft deletion.
- Added duplicate `InstitutionCode` validation and audit-field handling.
- Added category and country dropdowns plus all three multilingual institution names.

## 2. Key Files

- `OMM.Admin/Components/Pages/Admin/Institutions.razor`
- `OMM.Admin/Components/Pages/Admin/InstitutionEditModal.razor`
- `OMM.Admin/Components/Pages/Admin/DeleteInstitutionModal.razor`
- `OMM.Admin/Data/MasterDataDbContext.cs`
- `OMM.Public/Data/ApplicationDbContext.cs`
- `OMM.Public/Data/Migrations/20260831075809_RemoveMasterDataAuditForeignKeys.cs`

## 3. Data and Audit Rules

Institution create, update, status-toggle, and soft-delete operations retain the admin user ID in the audit fields. Master-data audit IDs are plain text because admin identity is in `admin.AspNetUsers` and master data is in `public`; the public identity foreign key is not valid for admin writes.

The migration removing the obsolete audit foreign keys has been applied to the configured database. It uses `DROP CONSTRAINT IF EXISTS` and `DROP INDEX IF EXISTS` because database environments may not contain the same historical constraints.

See [`../AGENTS.md`](../AGENTS.md) for the repeatable fix when this foreign-key error occurs again.

## 4. Verification

- `OMM.Admin` builds successfully.
- `OMM.Public` builds successfully.
- The database migration completed successfully.
- Add Institution was verified working after the migration.

## 5. Acceptance Criteria

- [x] Institution list, search, filters, sorting, paging, and summary cards.
- [x] Create and edit with code, multilingual names, category, country, and active status.
- [x] Duplicate-code validation.
- [x] Soft deletion with audit fields and query-filtered removal from the grid.
- [x] Active/inactive quick toggle with audit fields.
- [x] Build succeeds without compilation errors.

## 6. Pending Work

Phase 6 has no known implementation blockers. The next planned work is Phase 7:

- Replace free-text `Mine.Institution` input with an Institution-backed dropdown in `Mines.razor` and any other affected forms.
- Verify the database-backed stock lookup remains the default provider with JSON as an explicit fallback.

Admin user management is separately specified as Phase 7b in `docs/phase-7-admin-user-management.md`.

# Handoff — Execute Phase 3 Admin Layout & Navigation

Use this handoff together with [`phase-3-admin-layout.md`](phase-3-admin-layout.md).
The phase document is authoritative for the detailed requirements and acceptance
criteria. Do not infer additional features from this handoff.

## Current project state

- Solution: `OMMv2.slnx`
- Public project: `OMM.Public`
- Admin project: `OMM.Admin` (project boundary exists; Phase 3 functionality is
  still pending)
- Shared project: `OMM.Shared`
- Database: PostgreSQL on Neon, development branch only
- Database access: EF Core owns migrations per schema/store. `OMM.Public` owns
  shared master-data migrations; `OMM.Admin` owns its Admin Identity migrations.
  Dapper is used where appropriate for business/reference-data access
- Public stock lookup: supports `Database` and `Json` through
  `StockLookup:Provider`, with process-local `IMemoryCache` and configurable
  `StockLookup:CacheDays` (default 30)
- Phase 1: completed and merged
- Phase 2: completed and merged
- Phase 2b: required prerequisite; Admin Identity bootstrap is not yet complete
- Phase 3: next incomplete phase
- Phase 4: must wait until Phase 3 is complete

## Task

Execute `docs/phase-2b-admin-identity-bootstrap.md` first if its acceptance criteria
are not already verified. Then execute `docs/phase-3-admin-layout.md` exactly as
written. Build the separate admin
application shell, login boundary, authorization behavior, admin layout, dashboard,
and four authenticated stub routes.

## Hard boundaries

- Do not build `AdminDataGrid`; that is Phase 4.
- Do not build Stock or Institution CRUD; those are Phases 5–6.
- Do not build reference-data editing, user-management UI, maintenance controls,
  reports, or cache invalidation.
- Do not share public/admin Identity stores, cookies, or Data Protection keys.
- Do not add cross-application single sign-on.
- Do not create placeholder users or modify Phase 1 seed data.
- Do not create or apply a migration unless the phase document explicitly requires
  it and the target is a local/throwaway development database. Never use a shared or
  production database.
- Preserve the public application's existing login behavior except for the exact
  `Routes.razor` `NotAuthorized` correction required by Phase 3.

## Required verification

Before reporting completion:

1. Run `dotnet build` for the solution.
2. Run the public and admin applications as separate local processes.
3. Verify admin login and `/admin` as an authorized admin.
4. Verify all four stub routes.
5. Verify a logged-in non-admin reaches `/Account/AccessDenied`, not login and not
   an exception.
6. Verify a fully logged-out visitor is redirected to `/Account/Login`.
7. Report every Phase 3 acceptance criterion individually with evidence.

## Handoff instruction

Read `docs/phase-3-admin-layout.md` completely before editing. If any requirement is
ambiguous or conflicts with the repository, stop and ask the user rather than
guessing. When finished, report the changed files, verification results, and the
acceptance checklist item by item.

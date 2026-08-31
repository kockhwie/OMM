# Admin Backend & KLSE Master Data — Implementation Plan

> **This file is now an index, not the source of truth.** The original version of
> this file contained a KLSE schema draft that conflicted with an earlier, separately
> locked design (`docs/market-data-design.md`). That conflict has been resolved. This
> file now just points to the two documents that are actually authoritative, so there
> is exactly one place each kind of information lives — no duplicated content to drift
> out of sync.

## Where things live now

| Document | What it's for |
|---|---|
| [`market-data-design.md`](market-data-design.md) | **Locked schema.** Every table, every column, every naming decision for `Country`, `Exchange`, `Market`, `Sector`, `SubSector`, `Institution`, `Stock`. If you need to know a field name or type, it's here. |
| [`admin-backend-tasks.md`](admin-backend-tasks.md) | **Phase-by-phase task breakdown**, Phase 1 through 8, with acceptance criteria per phase and explicit dependency order. |
| [`phase-1-schema-migration.md`](phase-1-schema-migration.md) | **Standalone, self-contained Phase 1 doc.** Everything needed to execute Phase 1 (schema + migration + seed) is inlined here — hand this single file to an agent and they shouldn't need to open the other two. |
| [`phase-2-roles-superadmin.md`](phase-2-roles-superadmin.md) | **Public-only Identity phase.** Its roles, users, policies, and password behavior do not exist in Admin automatically. |
| [`phase-2b-admin-identity-bootstrap.md`](phase-2b-admin-identity-bootstrap.md) | **Admin-only Identity bootstrap.** PostgreSQL, roles, seed users, policies, lockout, and forced password change for `OMM.Admin`. |
| [`phase-3-admin-layout.md`](phase-3-admin-layout.md) | **Admin shell and navigation.** Starts only after Phase 2b is verified. |
| [`calculator-history-product-direction.md`](calculator-history-product-direction.md) | **Future product direction.** Defines guest calculator history, registration handoff, and the explicit “Save as Mine” flow. |

## Current architecture decisions

- The active solution is `OMMv2.slnx` with `OMM.Public`, `OMM.Admin`, and
  `OMM.Shared` projects in one repository. Public and admin applications are
  independently hostable and use separate Identity stores, cookies, secrets, and
  Data Protection keys.
- PostgreSQL on Neon is the current database platform. Use the Neon `development`
  branch for development and never run migrations against shared or production
  databases during development.
- EF Core is the migration owner for each schema/store: `OMM.Public` owns the
  shared market-data migrations and `OMM.Admin` owns its Admin Identity
  migrations. Dapper is used for business/reference-data access where
  appropriate; this is a hybrid design, not an EF replacement.
- Stock lookup supports `Database` and `Json` providers through
  `StockLookup:Provider`. The database provider is the default, while the existing
  JSON file remains available as a fallback. Lookup results use process-local
  `IMemoryCache` with a configurable `StockLookup:CacheDays` value (default 30).
- A future admin refresh action must account for separate app processes: clearing
  `OMM.Admin` memory does not clear `OMM.Public` memory. Secure cross-application
  invalidation is later work and must not be assumed to be provided by the current
  in-memory cache.

## Handing this to another AI agent (e.g. Codex)

Give the agent the relevant standalone phase document only, not this whole repo's
context. Each standalone phase document must identify the current project names,
database/provider decisions, scope boundaries, and exact acceptance criteria.

**Recommended process per phase:**
1. Work in a feature branch, not `main`.
2. Agent runs `dotnet build` and applies the migration to a local/throwaway DB only —
   never against a shared or production database without your explicit review.
3. Agent reports back against the phase's acceptance-criteria checklist explicitly
   (don't accept "done" without the checklist filled in).
4. You review the diff and the migration file by hand before merging — schema
   changes are the one category of change here that's expensive to walk back once
   real data is on top of it.
5. Only start the next phase after the current one is merged — phases have real
   dependencies (see the dependency notes in `docs/admin-backend-tasks.md`), not just
   suggested ordering.

## Current status

- [x] Schema reconciled and locked (`docs/market-data-design.md`)
- [x] Phase breakdown written (`docs/admin-backend-tasks.md`)
- [x] Phase 1 standalone doc ready to hand off (`docs/phase-1-schema-migration.md`)
- [x] Phase 1 executed and merged
- [x] Phase 2 executed and merged
- [x] Phase 2b Admin Identity bootstrap executed and merged
- [x] Phase 3 executed
- [x] Phase 4 executed
- [x] Phase 5 executed
- [x] Phase 6 executed
- [ ] Phase 7 executed

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
| [`docs/market-data-design.md`](docs/market-data-design.md) | **Locked schema.** Every table, every column, every naming decision for `Country`, `Exchange`, `Market`, `Sector`, `SubSector`, `Institution`, `Stock`. If you need to know a field name or type, it's here. |
| [`docs/admin-backend-tasks.md`](docs/admin-backend-tasks.md) | **Phase-by-phase task breakdown**, Phase 1 through 8, with acceptance criteria per phase and explicit dependency order. |
| [`docs/phase-1-schema-migration.md`](docs/phase-1-schema-migration.md) | **Standalone, self-contained Phase 1 doc.** Everything needed to execute Phase 1 (schema + migration + seed) is inlined here — hand this single file to an agent and they shouldn't need to open the other two. |
| [`docs/calculator-history-product-direction.md`](docs/calculator-history-product-direction.md) | **Future product direction.** Defines guest calculator history, registration handoff, and the explicit “Save as Mine” flow. |

## Handing this to another AI agent (e.g. Codex)

Give the agent **`docs/phase-1-schema-migration.md` only**, not this whole repo's
context. It's written to be fully self-contained for that one phase. Once Phase 1 is
reviewed and merged, a similar standalone doc should exist for Phase 2 before handing
that off — copy the pattern from `docs/admin-backend-tasks.md`'s Phase 2 section and
expand it the same way `phase-1-schema-migration.md` expanded Phase 1's.

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
- [ ] Phase 1 executed
- [ ] Phase 2 executed
- [ ] Phase 3 executed
- [ ] Phase 4 executed
- [ ] Phase 5 executed
- [ ] Phase 6 executed
- [ ] Phase 7 executed

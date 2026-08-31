# Copilot Instructions

## Project Guidelines
- For this repository, OMM.Admin uses admin-schema identities while shared master data lives in public schema. Master-data audit user IDs must be nullable text, not AspNetUsers foreign keys. When fixing legacy audit FK errors, update both EF mappings and the database migration, using PostgreSQL DROP CONSTRAINT IF EXISTS and DROP INDEX IF EXISTS for drift-safe migrations. Record this in AGENTS.md.
- For this repository, keep Phase 7 documentation limited to one final development document and one Phase 7 handoff document; avoid creating extra planning/task files unless explicitly requested.
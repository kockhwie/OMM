# Copilot Instructions

## Project Guidelines
- For this repository, OMM.Admin uses admin-schema identities while shared master data lives in public schema. Master-data audit user IDs must be nullable text, not AspNetUsers foreign keys. When fixing legacy audit FK errors, update both EF mappings and the database migration, using PostgreSQL DROP CONSTRAINT IF EXISTS and DROP INDEX IF EXISTS for drift-safe migrations. Record this in AGENTS.md.
- When adding repository instructions or handoff guidance, explicitly label the scope, especially when the instructions concern database/infrastructure log handling.
- For this repository, keep Phase 7 documentation limited to one final development document and one Phase 7 handoff document; avoid creating extra planning/task files unless explicitly requested.

## User Experience Preferences
- Prefer modern Blazor UI experiences for operational tasks such as viewing, searching, filtering, and downloading logs instead of PowerShell or command-line workflows.
- Ensure runtime-required directories are created automatically if missing, including recovery after accidental deletion, rather than requiring manual folder creation.
- For runtime/error-handling changes, do not stop at compilation; perform a practical end-to-end verification of the failure path, including generated folders/files, notification behavior, and user-visible fallback responses.
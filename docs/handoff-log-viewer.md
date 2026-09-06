# Handoff: Blazor Infrastructure Log Viewer

## Objective

Implement a modern Blazor log-management experience in `OMM.Admin`. Do not require PowerShell, curl, or manually opening hundreds of files.

The page should allow authorized operators to:

- View available database and infrastructure log files.
- Search log contents across files.
- Filter by date, application, exception type, and request path.
- Preview matching entries in a paginated or virtualized table.
- Download one log file.
- Download all selected logs as a ZIP archive.
- See file size, last modified time, and retention information.

## Current repository state

- `OMM.Admin` and `OMM.Public` share database availability and alerting code through `OMM.Shared`.
- Database failures are detected during startup and at request time.
- Failures are emailed through Resend using the `DatabaseAlerts` configuration section.
- Fallback logs are written locally under the configured `LogDirectory`, currently `App_Data/Logs`.
- The active file is `database-current.log`.
- Archived files use names such as `database-2026-09-07-045422-123.log` and rotate at 10 MB.
- The log viewer/download endpoints and emergency Basic Authentication were intentionally reverted. They must be designed and implemented again as part of this work.
- There is currently no Admin UI or HTTP endpoint for reading/downloading the fallback files.

Relevant shared code:

- `OMM.Shared/Infrastructure/DatabaseAlerting.cs`
- `OMM.Shared/Database/DatabaseAvailability.cs`
- `OMM.Admin/Program.cs`
- `OMM.Admin/appsettings.json`
- `OMM.Public/appsettings.json`

## Required security behavior

Normal access should require the existing `SuperAdmin` role. Do not expose log files through `OMM.Public`.

The viewer must also support database-outage access without relying on PostgreSQL or Identity. The preferred design is a separate operations authentication mechanism configured through deployment environment variables, not a credential stored in PostgreSQL. Do not put emergency credentials in source control, URLs, query strings, or `appsettings.json`.

Use HTTPS only. Log contents can contain sensitive exception details, so do not make the files public or cache them publicly.

## Database-outage requirements

The application may be unable to authenticate a normal Admin user when PostgreSQL is down. The log viewer therefore needs an independent emergency-access path. It must still be possible to:

1. Open the log viewer while the database is unavailable.
2. List files from the local fallback directory.
3. Search and preview file contents.
4. Download individual files or a ZIP archive.

The existing friendly database-unavailable page must not intercept the log viewer routes.

## Storage limitations

Render local disk is ephemeral. Files may disappear after a restart, redeploy, instance replacement, or hosting migration. The UI should clearly display this limitation and show the current storage location/retention behavior.

For long-term retention, consider Application Insights, Render logs, object storage, or another centralized logging provider. Do not assume local files are permanent backups.

## Suggested implementation direction

1. Extend the shared log writer with safe file enumeration, streaming reads, search, and ZIP creation.
2. Prevent path traversal by accepting only file names resolved inside the configured log directory.
3. Add Admin endpoints or a dedicated service layer that supports listing, searching, downloading, and ZIP generation.
4. Add a Blazor page under `OMM.Admin/Components/Pages/Admin`, for example `/admin/system/logs`.
5. Protect the page and API operations with `SuperAdmin` authorization plus the database-independent emergency operations authentication path.
6. Keep large-file operations streamed and bounded. Avoid loading all log files into memory.
7. Add clear empty, loading, unauthorized, unavailable, and error states.
8. Add tests for path traversal, filtering, rotation, ZIP selection, authorization, and operation when PostgreSQL is unavailable.

## Acceptance criteria

- A SuperAdmin can manage logs from the Blazor UI without command-line tools.
- The UI can search 1,000 or more log files without requiring manual file-by-file inspection.
- A user can download one file or a selected ZIP archive.
- No log route is exposed by `OMM.Public`.
- Database failure does not prevent emergency log access.
- Invalid file names cannot escape the configured log directory.
- Large logs do not cause unbounded memory usage.
- The full solution builds and the relevant tests pass.

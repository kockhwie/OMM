# Database Availability Incident Runbook

This operational runbook provides guidance for responding to PostgreSQL database outage alerts emitted by `OMM.Public` or `OMM.Admin`.

---

## 1. Alert Meaning and Root Cause Scope

When an email alert with the subject **"OMM database failure detected"** is received:

- **What it means**: An application instance failed to communicate with the PostgreSQL database. The application has marked itself as `Unavailable` and degraded gracefully to serve friendly 503 outage pages or safe Blazor circuit error messages rather than raw unhandled stack traces.
- **What it does NOT mean**: The alert does not diagnose the underlying root cause. Potential root causes include:
  - **Neon stale/rotated endpoint or compute suspend**: The Neon database branch or endpoint hostname changed, or the compute was paused and did not resume within the connection timeout.
  - **DNS resolution issues**: Render instances failing to resolve Neon's external domain name.
  - **Credential expiration or rotation**: Neon user password rotated without updating application configuration.
  - **Network partition**: Temporary network drop between Render (hosting provider) and Neon (database provider).
  - **Database saturation**: Connection pool exhaustion, long-running query locks, or compute memory limits reached.
  - **Fatal query failure**: Schema migration discrepancies or unhandled PostgreSQL engine aborts.

---

## 2. First Response: Checking Neon Connection String & Render Environment

In cloud deployments (Render + Neon), the most common operational failure is an expired, suspended, or rotated Neon database endpoint string.

### Step-by-Step Response:
1. **Check Neon Console**:
   - Log into the [Neon Console](https://console.neon.tech/).
   - Select the project and target branch (e.g. `main` or production branch).
   - Check the compute status (ensure it is **Active**, not stuck in error).
   - Copy the current, active **Connection String** (using pooled or direct connection as designated for the service).

2. **Compare with Render Environment Variables**:
   - Log into the [Render Dashboard](https://dashboard.render.com/).
   - Navigate to the affected service (`OMM.Public` or `OMM.Admin`).
   - Open **Environment** and inspect the `ConnectionStrings__DefaultConnection` (or `DefaultConnection`) variable.
   - Verify that the hostname, port, database name, username, and password match Neon's active connection string.

3. **Update and Redeploy**:
   - If the connection string was stale or incorrect, update the environment variable in Render.
   > [!IMPORTANT]
   > **Changing an environment variable in Render does NOT update already-running processes.**
   > You **must** trigger a manual deploy or restart of the service (`Manual Deploy` -> `Deploy latest commit` or `Restart Service`) so the application loads the new connection string into its configuration during startup.

---

## 3. Post-Incident Verification

Never assume a fix is successful based only on local workstation testing. Verify directly against the running Render deployment:

1. **Verify Database Health Endpoint**:
   - Send an HTTP request to the health check endpoint:
     ```bash
     curl -i https://<app-name>.onrender.com/health/db
     ```
   - **Expected Status**: `HTTP/1.1 200 OK` with JSON `{"status":"healthy"}`.
   - If still degraded, it returns `HTTP/1.1 503 Service Unavailable` with `{"status":"unavailable"}`.

2. **Verify HTML Route Delivery**:
   - Open the web application in a browser (e.g., `https://<app-name>.onrender.com/`).
   - Confirm that the application loads the home page rather than the 503 fallback template (`"We’ll be back shortly"`).

3. **Inspect Application Logs in Render**:
   - Look for the recovery log entries:
     ```
     Database connection restored. Database marked available.
     ```
   - Confirm that the background probe (`DatabaseAvailabilityMonitor`) executed `SELECT 1` cleanly and transitioned the process state back to available.

4. **Verify Interactive Functionality**:
   - Perform an interactive database action (e.g. searching stocks or navigating portfolio mines).
   - Confirm that Blazor circuits interact smoothly without triggering `<DatabaseErrorBoundary>`.

---

## 4. Alert Deduplication, Noise Expectations & Failure Isolation

- **At most one alert per outage transition**:
  - `OMM` uses atomic process-level state transitions (`available -> unavailable`).
  - While a process remains in the `Unavailable` state, repeated failed user requests and background probes **do not emit duplicate alert emails**.
  - A new alert is sent only after the database has fully recovered and then suffers a subsequent outage.
- **Process-local scope**:
  - Availability state is kept process-local. If multiple Render instances (e.g. horizontal autoscaling) experience an outage simultaneously, each process may emit an alert once for its own state transition.
- **Alert delivery resilience**:
  - If the email notification provider (e.g., Resend) fails or times out, the error is logged locally via `RollingDatabaseLogWriter` (`database-current.log`).
  - An email provider failure **never** replaces or masks the original database exception, ensuring application stability is preserved.

---

## 5. System Limitations & Operational Boundaries

- **No automatic configuration repair**:
  - The application cannot automatically fix a stale or invalid Neon hostname, rotate expired credentials, or modify Render environment variables. Operator intervention in Render and Neon is strictly required.
- **Circuit recovery requires user refresh**:
  - If an active Blazor Server circuit encounters a database error, `<DatabaseErrorBoundary>` isolates the component and displays a safe retry message. The circuit cannot automatically rewind state; the user must click **Refresh page**.
- **Process-isolated recovery**:
  - Background connectivity probes occur every 60 seconds (with an early probe on cold start). If the database resumes, the application restores availability automatically without needing a redeploy unless configuration was changed.


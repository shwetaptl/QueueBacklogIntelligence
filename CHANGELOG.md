# Changelog

All notable changes to Queue Backlog Intelligence System (QBIS) are documented here.

---

## [v1.4.0] — 2026-07-20

### Added

- **`CleanupFunction`** — new daily timer Azure Function (02:00 UTC, `TimerTrigger("0 0 2 * * *")`) that purges old rows from all three storage tables per configured retention windows: `QueueSnapshot` rows older than 24 hours, `QueueStatus` rows older than 90 days, `AlertRecord` rows older than 90 days; prevents unbounded table growth at the current ~1,440 writes/hour rate; logs per-queue and total deleted counts; individual queue failures are caught and logged without aborting the rest of the run
- **`PurgeOldSnapshotsAsync` / `PurgeOldStatusAsync` / `PurgeOldAlertRecordsAsync`** — three new `IRepository` / `Repository` methods; snapshot and status tables use reverse-tick RowKey range filter (`RowKey gt cutoffRowKey`) for efficient deletes without full-table scans; alert records use an OData `OpenedAtUtc lt datetime'...'` filter because their RowKey is a GUID with no time ordering; all deletes are batched in groups of 100 (Azure Table Storage transaction limit per batch)

### Fixed

- **`test_scenarios.sh` menu/function number mismatch** — interactive menu positions [6]–[12] did not match the functions they invoked or the banners those functions display; root causes: a fake "TC-08: Monitor Delay (not yet implemented)" placeholder blocked the real TC-08, and TC-06 Slow Drain had drifted to position [10] while TC-09 Burst was labelled "EXTRA"; fixed by removing the placeholder, inserting TC-06 at [7], and reassigning [7]–[11] so every menu number, label, and case branch aligns with the corresponding function banner (TC-06 through TC-10 in order)

---

## [v1.3.3] — 2026-07-16

### Fixed

- **DLQGrowth not detected when active queue empties** — `DLQGrowth` root cause check ran after the `ActiveCount == 0` early return, so a queue whose messages all expired to DLQ was always classified `Healthy`; moved DLQ check before the Healthy/Idle guard
- **DLQGrowth missed burst-fill pattern** — rate check `(DLQ[0] - DLQ[5]) / 5 > 1.0` only caught gradual growth; added `burstFilled` condition: DLQ currently high AND any of `snapshots[2..5]` shows `DLQCount == 0`, catching the common case where all messages expire at once
- **`Unknown` root cause incorrectly escalated to `Critical`** — when Monitor rate data was unavailable on the first snapshot after a burst, `SlaStatus` inherited `BREACHING` from recent history and `ComputeRawSeverity` returned `Critical` despite `RootCause = Unknown` (no evidence for the breach); added guard `result.RootCause != "Unknown"` on the BREACHING → Critical path

---

## [v1.3.2] — 2026-07-16

### Fixed

- **CORS — Authorization header blocked** — `AddCors()` in `DashboardFunction.cs` only listed `Content-Type` in `Access-Control-Allow-Headers`; added `Authorization` so Bearer tokens are no longer rejected by the browser preflight check; updated `host.json` to match
- **Login loop (block_nested_popups)** — switched `AuthProvider` from `loginPopup` to `loginRedirect` to avoid browser popup-blocking; switched `useAuthFetch` fallback from `acquireTokenPopup` to `acquireTokenRedirect`

---

## [v1.3.1] — 2026-07-14

### Added

- **KNOWN_ISSUES.md** — documents active known issues and system limitations: Azure Monitor ingestion delay, alert cooldown behaviour, single-namespace constraint, SMTP-only email, and SLA estimate accuracy

### Removed

- **`Dashboard.jsx`** — deleted orphaned file; `QueueDetail.jsx` replaced it in v1.2.0 and the old file was never referenced

---

## [v1.3.0] — 2026-07-13

### Added — Authentication (Azure AD + MSAL React)

- **Azure AD Easy Auth (backend)** — production API is now protected at the Azure Functions host level; tokens are validated by Azure before requests reach function code; zero backend code changes required (`AuthorizationLevel.Anonymous` remains — Easy Auth intercepts at the infrastructure layer)
- **MSAL React v5 (frontend)** — added `@azure/msal-browser` and `@azure/msal-react`; full login redirect flow with Microsoft account sign-in screen
- **`AuthProvider.jsx`** — wraps the app in `MsalProvider`; shows a "Sign in with Microsoft" screen when unauthenticated; displays login error messages inline; `VITE_AUTH_ENABLED=false` bypasses MSAL entirely for local dev
- **`useAuthFetch.js`** — hook that acquires a Bearer token silently on every API call; falls back to `acquireTokenRedirect` if the token has expired; all API calls in `useFetch` automatically receive the `Authorization` header
- **`msalConfig.js`** — MSAL instance config reading `VITE_AZURE_CLIENT_ID`, `VITE_AZURE_TENANT_ID`, `VITE_AZURE_API_SCOPE` from environment variables
- **Local dev bypass** — `VITE_AUTH_ENABLED=false` in `.env.local` skips the login gate completely; backend is open locally (Easy Auth is an Azure-only infrastructure feature)

### Added — Environment Variables

- `VITE_AUTH_ENABLED` — `true` to require login, `false` to bypass for local dev
- `VITE_AZURE_CLIENT_ID` — SPA app registration client ID
- `VITE_AZURE_TENANT_ID` — Azure AD directory (tenant) ID
- `VITE_AZURE_API_SCOPE` — API scope URI (`api://<api-client-id>/access_as_user`)

---

## [v1.2.1] — 2026-07-13

### Documentation

- Added `CHANGELOG.md` covering release history from v1.0.0 through v1.2.0

---

## [v1.2.0] — 2026-07-13

### Added — Frontend Dashboard Redesign

- **Sidebar navigation** — replaced top horizontal navbar with a fixed dark left sidebar showing QBIS brand, Overview and Settings nav links (active-state highlight), and a live backend health dot that polls `GET /health` every 60 seconds
- **Overview page** (`/`) — new multi-queue home screen showing:
  - Global alert banner (red for Critical, amber for Warning) with the worst-severity queue name, root cause, and a direct "View Queue →" link
  - 4 KPI tiles: Total Queues, Queues in Alert (split Critical / Warning), Total Active Messages, Dead Letter Queue count
  - Queue Health Grid — sortable table (Critical → Warning → OK) with one row per queue showing status badge, backlog count, trend, wait time, and root cause; each row is clickable and navigates to the queue detail page
- **Queue Detail page** (`/queues/:name`) — the existing single-queue deep-dive is now URL-driven via route parameter instead of a hardcoded queue name; includes a breadcrumb link back to Overview

### Fixed

- **Custom time range refresh bug** — clicking "Custom" in the Queue Activity chart time selector no longer triggers an immediate chart reload; date pickers are now draft state pre-filled with today's date, and the fetch only fires when the user clicks the new "Apply" button

### Backend

- Added `dlqCount` to `GET /api/queues` response so the Overview's Dead Letter Queue KPI tile has real data

---

## [v1.1.1] — 2026-07-13

### Fixed — Test Scenario Hardening

- **TC-01 (Empty Queue Baseline)** — increased wait from 150s to 180s to guarantee 2 analyzer cycles; removed "Unknown" from accepted trend values (Unknown means analyzer returned early — that is a failure); added explicit `alertSeverity = None` assertion
- **TC-03 (Growing Backlog)** — removed duplicate `save_snapshot` and `read -p` block caused by copy-paste error; users were being prompted twice
- **TC-05 Recovery** — changed assertion from `RootCause = Recovering` to `ANY/OK/None/ANY`; the Recovering state is a single-snapshot transient that disappears within one 30-second cycle if all messages are consumed at once, making the original assert flaky
- **TC-09 (Burst on Empty Queue)** — added `verify_not_consumer_stopped` assertion after burst phase; previously this phase had no assertion at all — it printed data but never failed the test
- **TC-10 (Full Lifecycle)** — added `verify_scenario` call after Phase 4 recovery wait; TC-10 was previously a demo walkthrough with zero assertions

### Added

- `verify_not_consumer_stopped()` helper function — queries the latest `QueueStatus` row and asserts `RootCause ≠ ConsumerStopped`, used to validate false-positive prevention scenarios (TC-08, TC-09)

---

## [v1.1.0] — 2026-07-13

### Added — Alerts

- **Teams dual-format alert support** — `AlertService` now detects the webhook URL type at runtime:
  - `webhook.office.com` URLs → legacy **MessageCard** JSON format (old Incoming Webhook connector)
  - All other URLs → **Adaptive Card** JSON format (new Microsoft Workflows connector)
- **Email alerts via SMTP** — new `SendEmailAlertAsync` method using `System.Net.Mail.SmtpClient` (built-in .NET, no new packages); sends responsive HTML email with header color matching severity (red / amber / green); reads SMTP settings from environment variables (`SmtpHost`, `SmtpPort`, `SmtpUser`, `SmtpPassword`, `SmtpFromAddress`); recipients configured per-queue via `EmailRecipients` (comma-separated)
- Teams and email alerts now fire in parallel via `Task.WhenAll`
- SMTP fields added to `local.settings.json.example` and `.env.example`

### Fixed

- **`idleThreshold` always-5 bug** — `Math.Min(Math.Max(recentPeak * 0.01, 5), 5)` always returned 5 regardless of queue peak, making the 1% relative idle threshold dead code; fixed to `Math.Max(recentPeak * 0.01, 5)` so high-peak queues (e.g. peak=1000) correctly use a relative threshold of 10 instead of 5
- **`Worker failed to load function: RenderOAuth2Redirect`** — `Microsoft.Azure.Functions.Worker.Extensions.OpenApi` v1.6.0 registers an internal function that conflicts with the isolated worker model causing a duplicate function ID crash on startup; removed the package entirely; all HTTP endpoints continue to work normally (only Swagger UI is removed)

### Removed

- `Microsoft.Azure.Functions.Worker.Extensions.OpenApi` NuGet package
- All `[OpenApiOperation]`, `[OpenApiParameter]`, `[OpenApiRequestBody]`, `[OpenApiResponseWithBody]`, `[OpenApiResponseWithoutBody]` attributes from `DashboardFunction.cs`
- OpenAPI service registration from `Program.cs`

---

## [v1.0.1] — 2026-06-20

### Documentation

- Added QueueConfig webhook verification step to `PROJECT_CONTEXT.md`
- Documented Azure Monitor fallback behavior when metrics are delayed

---

## [v1.0.0] — 2026-06-11

### Initial Release

Complete Queue Backlog Intelligence System for monitoring Azure Service Bus queues.

**Backend (Azure Functions v4, .NET 8 isolated worker)**
- `CollectorService` — polls Azure Service Bus Administration API every 30 seconds to collect `ActiveMessageCount`, `DeadLetterMessageCount`, incoming/outgoing rates via Azure Monitor Metrics
- `AnalyzerService` — classifies each snapshot into root causes: `ConsumerStopped`, `ConsumerSlowdown`, `ProducerSpike`, `ProducerSpikeAndConsumerSlowdown`, `DLQGrowth`, `Recovering`, `Healthy`; computes SLA breach status and trend label (`Growing`, `GrowingFast`, `Stable`, `Draining`, `DrainingFast`, `Idle`); uses smoothed 3-snapshot averages to filter Azure Monitor's 2–4 minute reporting delay
- `AlertService` — deduplication via cooldown window; severity escalation (Warning → Critical) triggers immediate re-alert; Teams webhook notifications
- `Repository` — Azure Table Storage persistence for `QueueStatus` (time-series snapshots) and `QueueIncident` (incident lifecycle with open/resolved tracking)
- REST API — 9 endpoints: `GET /queues`, `GET /queues/{name}/status`, `GET /queues/{name}/history`, `GET /queues/{name}/alerts`, `POST /queues`, `PUT /queues/{name}`, `DELETE /queues/{name}`, `GET /health`, `GET /version`

**Frontend (React 18, Vite, Recharts, Tailwind CSS)**
- Live status tiles: Active Count, Wait Time with SLA progress bar, Throughput (in/out/gap rates), Dead Letter Queue count
- Dual-panel activity chart: backlog + wait time (top) and message rates (bottom) with severity background bands and root cause change markers
- Root Cause Timeline — proportional color-coded strip showing how root cause changed over the selected time window
- Incident history table — filterable by date range and minimum duration, expandable rows with full incident details
- Time range selector: 15m / 30m / 1h / 6h / 24h / 7d / Custom
- Queue configuration UI: add, edit, enable/disable queues with inline validation

**Infrastructure**
- Docker Compose setup with nginx reverse proxy
- Azure Functions host configuration for 30-second timer triggers
- `.gitignore` excluding credentials (`local.settings.json`, `.env`)

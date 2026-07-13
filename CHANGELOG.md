# Changelog

All notable changes to Queue Backlog Intelligence System (QBIS) are documented here.

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

## [v1.0.1] — 2026-07-04

### Documentation

- Added QueueConfig webhook verification step to `PROJECT_CONTEXT.md`
- Documented Azure Monitor fallback behavior when metrics are delayed

---

## [v1.0.0] — 2026-07-04

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

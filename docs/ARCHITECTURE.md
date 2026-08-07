# Architecture & System Design

## Overview

Queue Backlog Intelligence (QBIS) is a serverless monitoring system built on Azure. It continuously collects metrics from Azure Service Bus, runs an intelligence pipeline to classify queue health, and surfaces findings through a REST API consumed by a React dashboard.

---

## Design Goals

| Goal | Decision |
|---|---|
| No polling from frontend | Timer-driven backend writes state; frontend reads pre-computed results |
| No false alerts | Sticky severity — requires 2 consecutive OK readings before de-escalation |
| Actionable alerts only | Alert deduplication: New / Escalation / Reminder / Recovery — never duplicates |
| Fast on-call triage | Dashboard answers "what, why, how long" in under 10 seconds |
| Predictive warnings | Warn before SLA breach using rate extrapolation |

---

## Component Map

```
┌─────────────────────────────────────────────────────────────────┐
│                        Azure Functions Host                      │
│                                                                  │
│  CollectorFunction     AnalyzerFunction    AlertDispatcher       │
│  (every :00s)          (every :30s)        (every :45s)          │
│       │                     │                    │               │
│       ▼                     ▼                    ▼               │
│  QueueSnapshot ──────► QueueStatus ──────► AlertRecord          │
│  (Table Storage)       (Table Storage)     (Table Storage)       │
│                              │                                   │
│                         DashboardFunction (HTTP)                 │
└─────────────────────────────────────────────────────────────────┘
         ▲                    ▲
         │                    │
  Azure Service Bus    Azure Monitor API
  (ActiveCount, DLQ)   (IncomingPerMin, OutgoingPerMin)

                              │
                         React Frontend
                         (React 18 + Vite)
```

---

## Data Flow

### Every minute (:00s) — CollectorFunction
1. Reads all enabled queues from `QueueConfig` table
2. Calls Azure Service Bus Management API → gets `ActiveCount`, `DLQCount`
3. Calls Azure Monitor Metrics API → gets `IncomingPerMin`, `OutgoingPerMin` (1–2 min lag)
4. Writes one `QueueSnapshot` row per queue

### Every 30 seconds (:30s) — AnalyzerFunction
1. Reads latest 2 snapshots per queue from `QueueSnapshot`
2. Runs the 9-step intelligence pipeline (see [ANALYZER_PIPELINE.md](ANALYZER_PIPELINE.md))
3. Writes one `QueueStatus` row per queue

### Every 45 seconds (:45s) — AlertDispatcherFunction
1. Reads latest `QueueStatus` per queue
2. Reads latest `AlertRecord` to check if alert was already sent
3. Decides alert type: New / Escalation / Reminder / Recovery / Suppress
4. Sends Teams Adaptive Card if needed
5. Writes `AlertRecord` row

### On HTTP request — DashboardFunction
- Reads from `QueueStatus` and `AlertRecord` tables
- Returns pre-computed data — no live computation on request

---

## Storage Schema

### Reverse Timestamp Pattern

All time-series tables use `RowKey = MaxTicks - DateTime.UtcNow.Ticks`. This means:
- Newer rows have **smaller** RowKey values
- `Take(1)` always returns the latest row without sorting
- Range queries scan forward from most-recent

### QueueConfig

| Field | Type | Description |
|---|---|---|
| PartitionKey | string | `"config"` |
| RowKey | string | Queue name |
| Namespace | string | `<name>.servicebus.windows.net` |
| SlaMinutes | int | Breach threshold |
| WarningThreshold | double | `0.60` = warn at 60% of SLA |
| CriticalThreshold | double | `1.0` = critical at 100% of SLA |
| TeamsWebhookUrl | string | Incoming webhook for alerts |

### QueueSnapshot

Raw metric sample written by CollectorFunction.

| Field | Type | Description |
|---|---|---|
| PartitionKey | string | Queue name |
| RowKey | string | Reverse timestamp |
| ActiveCount | long | Messages in queue |
| DlqCount | long | Dead-letter messages |
| IncomingPerMin | double | Rate from Azure Monitor |
| OutgoingPerMin | double | Rate from Azure Monitor |

### QueueStatus

Intelligence result written by AnalyzerFunction.

| Field | Type | Description |
|---|---|---|
| ActiveCount | long | Current active messages |
| WaitTimeMinutes | double | Estimated wait: ActiveCount ÷ OutgoingRate |
| TrendLabel | string | Growing / Draining / Stable / Idle |
| RootCause | string | ConsumerStopped / ProducerSpike / etc. |
| AlertSeverity | string | None / Info / Warning / Critical |
| GapPerMin | double | OutgoingRate − IncomingRate |
| Acceleration | double | Rate-of-change of ActiveCount delta |

### AlertRecord

Incident history written by AlertDispatcherFunction.

| Field | Type | Description |
|---|---|---|
| IncidentId | string | GUID — groups alerts for one incident |
| Status | string | Open / Resolved |
| PeakSeverity | string | Highest severity reached |
| FirstRootCause | string | Root cause at incident start |
| AlertCount | int | Number of notifications sent |
| DurationMinutes | int | Time from open to resolved |

---

## Frontend Architecture

```
App.jsx
└── Dashboard.jsx
    ├── IncidentBanner      — full-width alert (critical/warning only)
    ├── StatusRow           — 4 KPI tiles (activeCount, waitTime, throughput, DLQ)
    ├── ActiveCountChart    — two-panel synchronized chart
    │     Panel 1: backlog area + wait time line + severity bands + cause markers
    │     Panel 2: incoming/outgoing rate areas
    ├── RootCauseTimeline   — colour-coded horizontal cause strip
    └── IncidentTable       — filterable, sortable incident history
```

### Data fetching

All data fetches use the custom `useFetch(url, intervalMs)` hook which:
- Auto-refreshes on interval
- Clears stale data when URL changes (time range switch)
- Exposes `{ data, loading, error, fetchedAt, refetch }`

### API base URL

In local/Docker mode the React app calls `/api/...` and nginx forwards to the Functions container. In Azure deployment the app calls the Function App URL directly — `VITE_API_URL` is baked into the bundle at build time via the Vite environment variable.

---

## Deployment Modes

QBIS supports two distinct deployment paths. The codebase is identical in both; only the hosting layer differs.

### Mode 1 — Local development and self-hosted (Docker Compose)

```
Browser
  │
  ▼
nginx container (port 80/443)
  ├── /* → frontend container (React SPA, served by nginx)
  └── /api/* → backend container (Azure Functions, port 7071)
        │
        ▼
  Azure Table Storage (remote)
  Azure Service Bus (remote)
```

| Component | How it runs |
|---|---|
| Frontend | `frontend/Dockerfile` — Node builds `dist/`, nginx serves static files |
| Backend | `backend/Dockerfile` — .NET publishes, Functions host starts on 7071 |
| Reverse proxy | `frontend/nginx.conf` — forwards `/api/*` to backend container |
| Orchestration | `docker-compose.yml` (production) / `docker-compose.dev.yml` (hot reload) |
| Start command | `./start.sh` |

API URL in frontend: `/api/...` (relative — nginx handles routing, no hardcoded host).

### Mode 2 — Azure (live production deployment)

```
Browser
  │
  ▼
Azure Static Web App (CDN, HTTPS, global edge)
  │  serves React SPA (static files from dist/)
  │  VITE_API_URL points to Function App
  │
  ▼
Azure Function App (Consumption plan, .NET 8 isolated)
  │  CollectorFunction / AnalyzerFunction / AlertDispatcher / Cleanup / Dashboard
  │  protected by Azure AD Easy Auth
  │
  ▼
Azure Table Storage ── Azure Service Bus ── Azure Monitor API
```

| Component | How it runs |
|---|---|
| Frontend | `npm run build` → `dist/` deployed to Azure Static Web App via SWA CLI |
| Backend | `func azure functionapp publish` → source deployed to Azure Function App |
| Reverse proxy | None — Static Web App calls Function App URL directly via `VITE_API_URL` |
| SSL/TLS | Azure platform — no nginx needed |
| Docker | Not used — neither Dockerfile is involved in the Azure deployment path |

API URL in frontend: `https://qbi-function-app-ardvf6ffahaucwc0.centralus-01.azurewebsites.net/api` (absolute, set in `frontend/.env.production`).

### What changed and why

The original design used Docker + nginx as a self-contained portable stack that could run on any VM or local machine. When deployed to Azure, the platform provides the equivalent capabilities natively — Static Web Apps replaces nginx + frontend container, and the Functions runtime replaces the backend container. Docker is retained for local development and as a self-hosted fallback but is not part of the Azure deployment pipeline.

---

## Key Design Decisions

**Why Azure Table Storage instead of SQL?**
Table Storage is serverless, scales infinitely, and costs near-zero for this workload. The reverse-timestamp RowKey pattern ensures the most-recent row sorts first within a partition, so a `$top=1` query on a known PartitionKey seeks to the head of the partition's sorted index and returns without scanning the remaining rows. Lookup latency is therefore bounded by a single index seek rather than by the number of rows stored in the partition — a property of Azure Table Storage's sorted-key partition model, not a formally measured O(1) guarantee.

**Why isolated worker model for Azure Functions?**
The isolated worker runs in a separate process from the Functions host, giving full control over the .NET runtime version and dependency injection without host constraints.

**Why pre-compute on write rather than on read?**
Dashboard API reads are frequent (every 30s per browser tab). Computing intelligence on every read would be expensive and slow. Writing computed results means reads are simple table lookups.

**Why sticky severity?**
A single OK reading after a breach often means the consumer briefly caught up, not that the problem is resolved. Requiring 2 consecutive OK readings eliminates alert flapping.

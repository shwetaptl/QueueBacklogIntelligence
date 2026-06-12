# QBIS — Queue Backlog Intelligence System
## Complete Project Context Document

> This document is a full architecture reference. An AI with no codebase access
> should be able to understand, extend, or debug this project using only this file.

---

## 1. What This System Does

QBIS monitors an Azure Service Bus queue every minute and answers three questions:

1. **Is the queue healthy right now?** (active count, wait time vs SLA)
2. **Why is it unhealthy?** (consumer stopped? producer spike? DLQ growing?)
3. **Should an engineer be woken up?** (smart dedup — no alert storms)

It collects raw metrics every minute, runs a 9-step intelligence pipeline 30 seconds later, and dispatches alerts to Microsoft Teams 15 seconds after that. A React dashboard shows live status, time-series charts, and incident history.

---

## 2. Repository Layout

```
QueueBacklogIntelligence/
├── backend/                    ← .NET 8 Azure Functions (C#)
│   ├── Configuration/
│   │   └── CollectorSettings.cs
│   ├── Functions/
│   │   ├── CollectorFunction.cs       ← timer: every minute at :00
│   │   ├── AnalyzerFunction.cs        ← timer: every minute at :30
│   │   ├── AlertDispatcherFunction.cs ← timer: every minute at :45
│   │   └── DashboardFunction.cs       ← HTTP API (11 endpoints)
│   ├── Models/
│   │   ├── QueueConfigEntity.cs       ← Table Storage entity
│   │   ├── QueueSnapshotEntity.cs     ← Table Storage entity
│   │   ├── QueueStatusEntity.cs       ← Table Storage entity
│   │   └── AlertRecordEntity.cs       ← Table Storage entity
│   ├── Services/
│   │   ├── IRepository.cs             ← data access interface
│   │   ├── Repository.cs              ← Azure Table Storage impl
│   │   ├── CollectorService.cs        ← fetches metrics from Azure
│   │   ├── AnalyzerService.cs         ← 9-step intelligence pipeline
│   │   └── AlertService.cs            ← Teams webhook dispatcher
│   ├── Program.cs                     ← DI wiring + startup
│   ├── host.json                      ← Azure Functions host config
│   ├── local.settings.json            ← secrets (NEVER commit)
│   └── QueueBacklogIntelligence.csproj
│
├── frontend/                   ← React 18 + Vite + Tailwind CSS
│   └── src/
│       ├── main.jsx                   ← React entry point
│       ├── App.jsx                    ← NavBar + router shell
│       ├── hooks.js                   ← useFetch, useInterval, useSecondsAgo
│       ├── utils.js                   ← pure helper functions
│       ├── index.css                  ← Tailwind import
│       ├── pages/
│       │   ├── Dashboard.jsx          ← main monitoring page
│       │   └── Settings.jsx           ← queue CRUD management
│       ├── components/
│       │   ├── IncidentBanner.jsx     ← full-width alert bar (conditional)
│       │   ├── StatusRow.jsx          ← 4 KPI metric tiles
│       │   ├── ActiveCountChart.jsx   ← two-panel time-series chart
│       │   ├── TimeRangeSelector.jsx  ← preset + custom date picker
│       │   ├── RootCauseTimeline.jsx  ← colour-coded cause segments
│       │   ├── IncidentTable.jsx      ← sortable incident history table
│       │   ├── QueueForm.jsx          ← add/edit queue form
│       │   └── StatusCard.jsx         ← (legacy, not used in Dashboard)
│       └── context/
│           └── ToastContext.jsx       ← global toast notifications
│
├── start.sh                    ← starts backend + frontend together
└── PROJECT_CONTEXT.md          ← this file
```

---

## 3. Azure Infrastructure

| Resource | Name | Purpose |
|---|---|---|
| Resource Group | `qbi-rg` | Contains all resources |
| Service Bus Namespace | `qbi-sb-ns` | Message broker |
| Service Bus Queue | `qbi-queue` | The monitored queue |
| Storage Account | `queuebacklogsa` | Hosts all 4 Table Storage tables |
| Azure Functions App | (local dev) | Runs all 4 functions |

### Azure Table Storage — 4 tables

| Table | PartitionKey | RowKey | Purpose |
|---|---|---|---|
| `QueueConfig` | `"config"` | queue name | One row per monitored queue — configuration |
| `QueueSnapshot` | queue name | reverse timestamp (D19) | Raw metrics collected every minute |
| `QueueStatus` | queue name | reverse timestamp (D19) | Computed intelligence written every minute |
| `AlertRecord` | queue name | incident GUID | One row per incident (not per notification) |

**Reverse timestamp pattern:** `RowKey = (DateTime.MaxValue.Ticks - now.Ticks).ToString("D19")`
- Newer rows have **smaller** RowKey values
- Table Storage always sorts ascending by RowKey
- Result: queries with `maxPerPage: N` return the **N most recent rows** without a sort pass
- Range queries: for `[from, to]` date range → `lowerRowKey = MaxTicks - to.Ticks`, `upperRowKey = MaxTicks - from.Ticks`

---

## 4. Backend — Azure Functions

### Runtime
- **.NET 8 Isolated Worker** (`AzureFunctionsVersion v4`)
- Entry point: `Program.cs` — `FunctionsApplication.CreateBuilder(args)`
- DI container: `Microsoft.Extensions.DependencyInjection`
- All services registered as **singletons**

### DI Wiring (Program.cs)
```
ServiceBusAdministrationClient  ← connection string from env
MetricsQueryClient               ← DefaultAzureCredential (Managed Identity / az login)
TableServiceClient               ← connection string from env
IRepository → Repository         ← wraps TableServiceClient
CollectorService                 ← injected with SBAdminClient + MetricsQueryClient
AnalyzerService                  ← stateless, injected into AnalyzerFunction
AlertService                     ← injected with IRepository + IHttpClientFactory
IHttpClientFactory               ← AddHttpClient()
```

### Required Environment Variables (local.settings.json)
```json
{
  "ServiceBusConnectionString": "Endpoint=sb://...",
  "StorageConnectionString":    "DefaultEndpoints...",
  "AzureWebJobsStorage":        "(same as StorageConnectionString)",
  "FUNCTIONS_WORKER_RUNTIME":   "dotnet-isolated",
  "EnableSeedData":             "true"
}
```
⚠️ **`local.settings.json` must never be committed** — it contains production connection strings.

---

## 5. The 3-Function Pipeline (runs every minute)

```
:00  CollectorFunction      → writes QueueSnapshot to Table Storage
:30  AnalyzerFunction       → reads snapshots → runs 9-step pipeline → writes QueueStatus
:45  AlertDispatcherFunction → reads QueueStatus → sends Teams webhook if NeedToSendAlert=true
```

### CollectorFunction (timer: `0 */1 * * * *`)
Runs in parallel across all enabled queues. For each queue:
1. Calls `ServiceBusAdministrationClient.GetQueueRuntimePropertiesAsync()` → `ActiveCount`, `DLQCount`
2. Calls `MetricsQueryClient.QueryResourceAsync()` for `IncomingMessages` + `OutgoingMessages` (last 3 min, 1-min granularity) → `IncomingPerMin`, `OutgoingPerMin` (nullable — Azure Monitor can be delayed)
3. Writes `QueueSnapshotEntity` to `QueueSnapshot` table

### AnalyzerFunction (timer: `30 */1 * * * *`)
Runs 30s after Collector to ensure the snapshot is written first. For each queue:
1. Reads last 22 snapshots (`AnalyzerService.SnapshotCount = 22`)
2. Reads recent status history (`SlaMinutes + 5` rows)
3. Calls `AnalyzerService.Analyze()` → produces `QueueStatusEntity`
4. Writes result to `QueueStatus` table

### AlertDispatcherFunction (timer: `45 * * * * *`)
Runs 15s after Analyzer. For each queue:
1. Reads latest `QueueStatus`
2. Checks `NeedToSendAlert` flag
3. If true: builds Teams Adaptive Card, POSTs to webhook, updates `AlertRecord`

---

## 6. AnalyzerService — 9-Step Intelligence Pipeline

**Key design principle:** `ActiveCount` from Service Bus Admin API is **ground truth**. Azure Monitor rates (`IncomingPerMin`, `OutgoingPerMin`) are **secondary** — they have 1-2 min delay and can have artifacts. All state decisions start with ActiveCount delta.

### Constants
| Constant | Value | Why |
|---|---|---|
| `SnapshotCount` | 22 | Covers 20 min history + 2 missed-run buffer |
| `RateWeights` | [0.50, 0.30, 0.20] | 50% latest, 30% prev, 20% oldest — fast crash detection |
| `NoiseFloorPct` | 5% of ActiveCount | Natural measurement variance |
| `NoiseFloorMin` | 1.0 messages | Prevents over-sensitivity on tiny queues |
| `NoiseFloorMax` | 10.0 messages | Prevents under-sensitivity on large queues |
| `ConsistencyTolerance` | 5× noiseFloor | How much Monitor can differ from Admin API before distrusted |
| `RootCauseThreshold` | 25% | Min rate change from baseline to classify a root cause |
| `DeEscalationRequiredCount` | 2 | Consecutive OK readings required before dropping severity |

### Step 1: Compute ActiveCount Deltas
- Compute delta per minute for up to 6 consecutive snapshot pairs
- Skip gaps > 3 minutes (collector missed a run)
- `netRateNow` = latest delta
- `netRateSmoothed` = weighted average of up to 3 deltas (using RateWeights)
- `acceleration` = `deltas[0] - deltas[1]` (is situation worsening or improving?)
- Stored in `GapPerMin` and `Acceleration` on result

### Step 2: Dynamic Noise Floor
`noiseFloor = Clamp(ActiveCount × 0.05, 1.0, 10.0)`

### Step 3: Classify Queue State (5 mutually exclusive states)
| State | Condition |
|---|---|
| **Idle** | ActiveCount ≤ threshold AND no movement AND Monitor rates near 0 AND previous snapshot also small |
| **ConsumerStopped** | NOT idle, NOT draining, NOT recently empty (2 min), Monitor shows Out < 1 or null for 2+ min |
| **Draining** | netRateSmoothed ≤ −noiseFloor |
| **Growing** | netRateSmoothed > noiseFloor |
| **Stable** | None of the above |

`queueWasRecentlyEmpty` suppresses ConsumerStopped for 2 min after queue was empty — handles burst arrival where Monitor hasn't caught up yet.

### Step 4: Derive Reliable Outgoing Rate
Strategy depends on state:
- **Idle/Empty** → 0
- **ConsumerStopped** → 0
- **Pure drain** (draining + incoming < 1) → `abs(netRateNow)` — exact, no Monitor needed
- **Other** → try weighted Monitor rates with consistency check; if inconsistent, use last known good rate from history; if none, null (honest uncertainty)

### Step 5: WaitTime and SLA Status
`WaitTime = ActiveCount / OutgoingRate`
- Empty/Idle → `0, OK`
- ConsumerStopped → `null, BREACHING` (never fake a number)
- Rate ≥ 0.5 → exact calculation
- Rate < 0.5 → `null, BREACHING` (effectively stopped)
- No rate → `null`, check recent history for BREACHING/UNKNOWN

### Step 6: TrendLabel (7 values)
`Idle | Stable | Growing | GrowingFast | Draining | DrainingFast | Unknown`
- `GrowingFast` = Growing AND acceleration > noiseFloor AND ActiveCount > 5
- `DrainingFast` = Draining AND acceleration < −noiseFloor

### Step 7: RootCause (8 values, priority order)
`Healthy | ConsumerStopped | ConsumerSlowdown | ProducerSpike | ProducerSpikeAndConsumerSlowdown | DLQGrowth | Recovering | Unknown`

Priority:
1. **Recovering** — check FIRST: was ConsumerStopped in last 6 status rows AND now draining
2. **ConsumerStopped** — from Step 3
3. **Healthy** — if idle or empty
4. **DLQGrowth** — DLQ rate > 1/min over last 5 snapshots AND count > 5
5. **Rate-based** — requires Monitor data + 3 snapshot baseline (5-15 min old)
   - Baseline = median of rate snapshots from 5-15 min ago (median is robust to outliers)
   - `inChange > 25%` → ProducerSpike; `outChange < -25%` → ConsumerSlowdown; both → combined
6. **Unknown** — insufficient data

### Step 8: AlertSeverity (3 values)
`None | Warning | Critical`

| Condition | Severity |
|---|---|
| Idle or empty | None |
| ConsumerStopped | Critical (always) |
| WaitTime/SLA ≥ criticalThreshold (default 1.0) | Critical |
| WaitTime/SLA ≥ warningThreshold (default 0.75) | Warning |
| Growing + breach predicted within 1 SLA duration | Warning (predictive) |
| SLA BREACHING with no wait time | Critical |

**Sticky severity:** requires `DeEscalationRequiredCount = 2` consecutive OK readings before de-escalating. Prevents single-minute Monitor artifact from triggering false recovery.

**Predictive warning formula:**
- `waitTimeGrowthRate = netRateSmoothed / derivedOutgoing`
- `timeToBreach = (slaMinutes - currentWaitTime) / waitTimeGrowthRate`
- If `timeToBreach < slaMinutes` → fire Warning now

### Step 9: NeedToSendAlert
| Rule | Condition |
|---|---|
| New incident | Previous severity was None, now Warning/Critical |
| Escalation | Previous was Warning, now Critical → always send, bypass cooldown |
| Reminder | Same severity, `minutesSinceLast ≥ cooldownMinutes` |
| Recovery | Was Warning/Critical, now None AND `SlaStatus=OK` AND not Growing |
| Suppress | Same severity within cooldown → do NOT send |

---

## 7. AlertService — Teams Webhook

Triggered only when `NeedToSendAlert = true`.

**Alert types:** `NewIncident | Escalation | Reminder | Recovery`

**Teams payload:** Microsoft Adaptive Card format (`application/vnd.microsoft.card.adaptive`)
- For active alerts: shows title, Root Cause, Active Messages, Wait Time, Trend as a FactSet
- For recovery: shows incident duration

**AlertRecord lifecycle:**
- `NewIncident` → creates new row in `AlertRecord` table with `Status=Open`
- `Escalation` → updates `PeakSeverity=Critical`, increments `AlertCount`
- `Reminder` → increments `AlertCount`, updates `LastAlertSentUtc`
- `Recovery` → sets `Status=Resolved`, sets `ResolvedAtUtc`

---

## 8. Data Models

### QueueConfigEntity
```
PartitionKey     = "config"
RowKey           = queue name (primary key)
QueueName        string
Namespace        string     (Service Bus namespace, e.g. qbi-sb-ns)
SubscriptionId   string     (Azure subscription GUID)
ResourceGroupName string
SlaMinutes       int        (default 15 — breach threshold)
IsEnabled        bool       (only enabled queues are collected/analyzed)
CooldownMinutes  int        (default 10 — reminder interval)
WarningThreshold double     (default 0.75 — 75% of SLA)
CriticalThreshold double    (default 1.0 — 100% of SLA)
TeamsWebhookUrl  string?    (optional — Teams incoming webhook)
EmailRecipients  string?    (optional — comma-separated, not yet implemented)
```

### QueueSnapshotEntity
```
PartitionKey     = queue name
RowKey           = reverse timestamp D19
TimestampUtc     DateTime
ActiveCount      long       (from Service Bus Admin API — ground truth)
DLQCount         long       (from Service Bus Admin API)
IncomingPerMin   double?    (from Azure Monitor — nullable, can be delayed)
OutgoingPerMin   double?    (from Azure Monitor — nullable, can be delayed)
```

### QueueStatusEntity
```
PartitionKey     = queue name
RowKey           = reverse timestamp D19
TimestampUtc     DateTime
ActiveCount      long
DLQCount         long
IncomingPerMin   double?    (may be overridden by derived value)
OutgoingPerMin   double?    (may be overridden by derived value)
WaitTimeMinutes  double?    (null = infinite or unknown)
SlaMinutes       int        (copied from config for dashboard use)
SlaStatus        string     "OK" | "BREACHING" | "UNKNOWN"
GapPerMin        double?    (net backlog change/min — positive=growing)
Acceleration     double?    (rate of change of rate)
TrendLabel       string     "Idle"|"Stable"|"Growing"|"GrowingFast"|"Draining"|"DrainingFast"|"Unknown"
RootCause        string     "Healthy"|"ConsumerStopped"|"ConsumerSlowdown"|"ProducerSpike"|
                            "ProducerSpikeAndConsumerSlowdown"|"DLQGrowth"|"Recovering"|"Unknown"
AlertSeverity    string     "None" | "Warning" | "Critical"
NeedToSendAlert  bool       (true only this minute — dedup already applied)
```

### AlertRecordEntity
```
PartitionKey     = queue name
RowKey           = incident GUID
IncidentId       string     (GUID)
OpenedAtUtc      DateTime
ResolvedAtUtc    DateTime?  (null if still open)
Status           string     "Open" | "Resolved"
PeakSeverity     string     "Warning" | "Critical"
FirstRootCause   string
LastAlertSentUtc DateTime
AlertCount       int        (total notifications sent for this incident)
```

---

## 9. HTTP API — DashboardFunction

**Base URL:** `http://localhost:7071/api` (local) | `https://<app>.azurewebsites.net/api` (production)

All endpoints: `AuthorizationLevel.Anonymous`. CORS: `*` (all origins).

OpenAPI/Swagger available at: `http://localhost:7071/api/swagger/ui`

### GET /api/queues
All configured queues with live status. Used by Settings page.
```json
[{ "queueName","namespace","subscriptionId","resourceGroupName","slaMinutes","isEnabled",
   "cooldownMinutes","warningThreshold","criticalThreshold","teamsWebhookUrl","emailRecipients",
   "activeCount","waitTimeMinutes","slaStatus","trendLabel","rootCause","alertSeverity",
   "gapPerMin","acceleration","lastUpdatedUtc" }]
```

### GET /api/queues/{queueName}/status
Latest computed status. Polled every 30s by Dashboard.
```json
{ "queueName","isEnabled","slaMinutes","activeCount","waitTimeMinutes","slaStatus",
  "trendLabel","rootCause","alertSeverity","gapPerMin","acceleration","dlqCount",
  "incomingPerMin","outgoingPerMin","lastUpdatedUtc" }
```

### GET /api/queues/{queueName}/history
Time-series status history for charts. Ordered oldest-first.
- `?minutes=N` — last N minutes (1–10080, default 30)
- `?from=ISO&to=ISO` — explicit UTC range (max 90 days)

```json
[{ "timestampUtc","activeCount","waitTimeMinutes","slaStatus","alertSeverity",
   "trendLabel","rootCause","gapPerMin","incomingPerMin","outgoingPerMin" }]
```

### GET /api/queues/{queueName}/snapshots
Raw collector snapshots. Same query params as /history.
```json
[{ "timestampUtc","activeCount","incomingPerMin","outgoingPerMin","dlqCount" }]
```

### GET /api/queues/{queueName}/alerts
All alert incidents ordered newest first.
```json
[{ "incidentId","openedAtUtc","resolvedAtUtc","status","peakSeverity",
   "firstRootCause","alertCount","durationMinutes" }]
```

### POST /api/queues — create queue
### PUT /api/queues/{queueName} — update queue
Request body (both):
```json
{ "queueName","namespace","subscriptionId","resourceGroupName","slaMinutes",
  "isEnabled","cooldownMinutes","warningThreshold","criticalThreshold",
  "teamsWebhookUrl","emailRecipients" }
```
Responses: `200 {message}` | `400 {error}` | `404` | `409` (conflict on create)

### DELETE /api/queues/{queueName}
Responses: `200 {message}` | `404`

### GET /api/health
```json
{ "status":"Healthy"|"Degraded", "lastCollectorRunUtc","lastAnalyzerRunUtc",
  "collectorDelayMinutes","analyzerDelayMinutes", "message" }
```
Degraded if either function hasn't run in > 3 minutes.

### OPTIONS /{*route}
CORS preflight — returns 200 for all routes.

---

## 10. Frontend Architecture

### Stack
- **React 18.3** + **Vite 6** + **Tailwind CSS 4**
- **React Router DOM 7** — client-side routing, no page reloads
- **Recharts 2.13** — charting library

### Entry Point
```
index.html  →  main.jsx  →  App.jsx  →  pages
```
`main.jsx` mounts React into `<div id="root">`, wraps in `BrowserRouter` + `React.StrictMode`.

### App.jsx — Shell
Permanent frame. Contains:
- **NavBar** — brand, Dashboard/Settings links (active state via `useLocation`), live health dot (polls `/api/health` every 60s)
- **Router** — `/ → Dashboard`, `/settings → Settings`
- **ToastProvider** — wraps everything, provides toast context to all children

### hooks.js — Data Layer

#### `useFetch(url, intervalMs)`
Core data-fetching hook. Returns `{ data, loading, error, fetchedAt, refetch }`.
- Fires immediately on mount
- Auto-refreshes every `intervalMs` ms (pass `null` to disable)
- **URL change behaviour:** when URL changes (time range switch), immediately sets `data=null, loading=true` — clears stale data before new data arrives
- Error handling: sets `error` message, keeps `loading=false` so UI shows error state

#### `useSecondsAgo(date)`
Returns live counter of seconds since `date`. Updates every second via `useInterval`.

#### `useInterval(callback, delay)`
Stable interval hook using `useRef` to avoid stale closures.

### utils.js — Pure Helpers
```js
cardBg(severity)       → Tailwind class string for card background/border
lineColor(severity)    → hex colour for chart line
trendArrow(trend)      → '↑↑' | '↑' | '→' | '—' | '↓' | '↓↓'
signed(n)              → '+2.5' | '-1.0' | '0.0'
parseUTC(iso)          → Date | null (handles missing Z)
fmtHHMM(iso)           → 'HH:MM' local time
fmtDateTime(iso)       → '2026-05-05 22:01 UTC'
rootCauseLabel(cause)  → 'ConsumerStopped' → 'Consumer Stopped'
rootCauseColor(cause)  → hex colour per root cause
```

Root cause colour map:
```
Healthy                         → #22C55E (green)
ConsumerStopped                 → #EF4444 (red)
ConsumerSlowdown                → #F97316 (orange)
ProducerSpike                   → #A855F7 (purple)
ProducerSpikeAndConsumerSlowdown → #DC2626 (dark red)
DLQGrowth                       → #F59E0B (amber)
Recovering                      → #3B82F6 (blue)
Unknown                         → #9CA3AF (gray)
```

---

## 11. Frontend Pages

### Dashboard.jsx
**Owns all data fetching for the monitoring view.** Single source of truth.

State:
```js
timeRange     { preset: '30m' | '15m' | '1h' | '6h' | '24h' | '7d' | 'custom',
                from?: 'YYYY-MM-DD', to?: 'YYYY-MM-DD' }
dismissedSev  string | null   — severity the user has acknowledged
```

Fetches (all auto-refresh):
```js
statusR    = useFetch(`/queues/qbi-queue/status`,          30_000ms)
historyR   = useFetch(`/queues/qbi-queue/history${tq}`,    30_000ms)
alertsR    = useFetch(`/queues/qbi-queue/alerts`,          60_000ms)
```

`tq` = `?minutes=N` or `?from=ISO&to=ISO` depending on `timeRange`.

URL changes when `timeRange` changes → `useFetch` clears stale data and reloads.

`openAlert` = first alert from `alertsR.data` with `status === 'Open'`
`breachStartMs` = `openAlert?.openedAtUtc` as milliseconds

Render tree:
```
IncidentBanner   (statusR.data, breachStartMs, dismissedSev)
StatusRow        (statusR.data, breachStartMs, onRefresh)
ActiveCountChart (historyR.data, slaMinutes, alertSeverity, timeRange)
RootCauseTimeline(historyR.data)
IncidentTable    (alertsR.data)
```

### Settings.jsx
Queue CRUD management. Uses `React.memo`, `useMemo`, `useCallback` for performance.
- `useFetch('/queues', null)` — no auto-refresh; manually calls `refetch` after mutations
- Edit mode: opens `QueueForm` pre-populated; queue name disabled (it's the PK)
- Add mode: opens empty `QueueForm`
- Delete: `window.confirm` → `DELETE /api/queues/{name}` → reload

---

## 12. Frontend Components

### IncidentBanner.jsx
Renders **only** when `alertSeverity === 'Warning' | 'Critical'` AND not dismissed.

Props: `{ status, breachStartMs, dismissedSev, onDismiss }`

Behaviour:
- Red background for Critical, amber for Warning
- Shows: severity badge, live breach duration (via `useSecondsAgo`), root cause label, current wait time
- "Acknowledge" button calls `onDismiss` → Dashboard sets `dismissedSev = currentSev`
- Auto-reappears if severity changes (escalation or new incident)
- Disappears automatically when severity returns to None

### StatusRow.jsx
4 horizontal KPI tiles, each with severity-coloured border.

Props: `{ status, loading, error, fetchedAt, breachStartMs, onRefresh }`

Tiles:
1. **Active Count** — large number + trend label + gap/min rate
2. **Wait Time** — value + animated progress bar (% of SLA), colour: green <60%, amber 60-100%, red >100%
3. **Throughput** — Incoming/min (purple), Outgoing/min (green), Gap/min (red/green)
4. **Dead Letter Queue** — count, red when > 0 with "Poison messages — investigate" label

Header: queue name, severity badge, root cause, breach duration, last updated, refresh button.

### ActiveCountChart.jsx
**Two-panel synchronized chart** (both panels share `syncId="qbi-chart"` for hover sync).

Props: `{ history, slaMinutes, alertSeverity, loading, error, timeRange, onTimeRangeChange }`

**Panel 1 — Backlog & SLA (190px height):**
- `Area` for `activeCount` — filled area, colour from `lineColor(alertSeverity)`
- `Line` (dashed) for `waitTime` — right Y-axis
- `ReferenceLine` for SLA threshold (red dashed)
- `ReferenceArea` for severity bands (red/amber background shading on Critical/Warning periods)
- `ReferenceLine` for each root cause transition (vertical dashed, abbreviated label: CS/PS/DLQ/REC)
- X-axis hidden (grid lines only)
- Y-axis domain: `[0, max(dataMax * 1.3, 5)]` — always shows some range even when idle

**Panel 2 — Message Rate (110px height):**
- `Area` for `incomingPerMin` (purple, 15% opacity fill)
- `Area` for `outgoingPerMin` (green, 15% opacity fill)
- X-axis with time labels
- Tooltip suppressed (top panel's tooltip shows all data including rates)

**Time bucketing:** Generates N evenly-spaced buckets spanning `[fromMs, toMs]`. Each bucket holds the nearest real data point or `null`. Category axis (no `type="number"`) ensures tooltip activates on every bucket including empty ones.

**Bucket sizes per preset:**
```
15m → 1 min  (15 buckets)     1h  → 2 min  (30 buckets)
30m → 1 min  (30 buckets)     6h  → 10 min (36 buckets)
24h → 30 min (48 buckets)     7d  → 4 hrs  (42 buckets)
```
Custom ranges use `customBucketMs(fromMs, toMs)` which scales by day count (2min → 12hr).

**Tooltip** shows: time, active count, wait time + SLA%, root cause, severity, in/out rates.

**Empty state:** Green notice "Queue idle — no messages in this period" when all activeCount = 0.

### TimeRangeSelector.jsx
Preset buttons: `15m | 30m | 1h | 6h | 24h | 7d | Custom`

When Custom clicked: defaults both date pickers to today (`new Date().toISOString().slice(0,10)`). User changes from/to dates, each change fires `onChange({ preset: 'custom', from, to })`.

Value shape: `{ preset: string }` or `{ preset: 'custom', from: 'YYYY-MM-DD', to: 'YYYY-MM-DD' }`

### RootCauseTimeline.jsx
Colour-coded horizontal strip below the chart.

Props: `{ history, onSegmentClick }`

Algorithm:
1. Sort history by `timestampUtc`
2. Walk rows, group consecutive same-`rootCause` rows into segments
3. Each segment: `{ cause, fromIso, toIso, fromMs, toMs }`
4. Render as flex divs, width proportional to duration
5. Hover shows tooltip with full cause label + start/end timestamps
6. Click calls `onSegmentClick(segment)` — Dashboard can use to filter IncidentTable

Abbreviated cause labels: `Healthy=OK, ConsumerStopped=CS, ConsumerSlowdown=CS↓, ProducerSpike=PS, ProducerSpikeAndConsumerSlowdown=PS+CS, DLQGrowth=DLQ, Recovering=REC`

### IncidentTable.jsx
Sortable, filterable, expandable incident history.

Props: `{ alerts, loading, error }`

State: `fromDate` (30 days ago), `toDate` (today), `minDuration`, `sortAsc` (false = newest first), `expandedId`

Filters applied client-side. Default: last 30 days, no minimum duration, newest-first sort.

Click "Started" header → toggles `sortAsc`. Click row → expands to show `incidentId`, exact opened/resolved timestamps, total alerts sent.

Root cause shown via `rootCauseLabel()` — human-readable.

Bug note: initial `toDate` uses `useState(todayStr())` — **must include `()` call** — was a bug where function reference was stored instead of today's date string.

### QueueForm.jsx
Controlled form for creating/editing queue configurations.

Props: `{ queue, onSave, onCancel, saving }`

- Add mode: `queue = null` → empty form
- Edit mode: `queue = {...}` → pre-populated, `queueName` disabled (PK)
- `useEffect([queue])` populates form when switching between queues to edit
- Single `handleChange` handler for all fields (`e.target.name` dispatch)
- Validation on submit: all fields required except `teamsWebhookUrl` + `emailRecipients`. Shows errors only for touched fields.
- Converts string inputs to `Number()` before calling `onSave()`

### ToastContext.jsx
Global notification system.

- `ToastProvider` wraps `App` — owns `toast` state
- `showToast(message, type)` — `type = 'success' | 'error'`
- Auto-dismisses after 3 seconds
- Fixed bottom-right, `z-50`
- Used by Settings.jsx after save/delete operations

---

## 13. Important Invariants and Design Decisions

1. **ActiveCount is always ground truth.** Never use Monitor rates to override Admin API active count.

2. **Never fabricate a WaitTime.** If outgoing rate is unreliable, `WaitTimeMinutes = null`. A null with `SlaStatus=BREACHING` is more honest than a wrong number.

3. **ConsumerStopped requires BOTH conditions.** ActiveCount not dropping (Admin API) AND Monitor confirms Out near zero. Either alone is insufficient.

4. **The 2-minute burst suppression window.** After queue was empty, ConsumerStopped is suppressed for 2 min because Azure Monitor hasn't caught up yet.

5. **Sticky severity.** Requires 2 consecutive OK readings to de-escalate. One good reading can be a Monitor artifact.

6. **Alert deduplication.** `NeedToSendAlert` is false within cooldown window. Escalation bypasses cooldown (always notify). Recovery only fires when genuinely resolved (SlaStatus=OK AND not Growing).

7. **RowKey reverse timestamp.** Azure Table Storage sorts ascending. Reverse ticks makes newest rows sort first so `maxPerPage: N` returns N most recent rows efficiently.

8. **One AlertRecord per incident, not per notification.** `AlertCount` tracks how many notifications were sent for a single incident. This is what appears in IncidentTable as "Alerts Sent".

9. **Frontend: URL change clears data.** `useFetch` has `useEffect([url]) { setData(null); setLoading(true) }`. When timeRange changes, old chart data disappears immediately rather than persisting until new data arrives.

10. **Median for baseline, not mean.** Root cause uses median of historical rates as baseline. Median is robust to outliers (e.g., a spike in one of the baseline minutes won't skew the baseline).

---

## 14. Testing

Testing is **manual injection via `backend/Tests/test_scenarios.sh`** — an interactive bash script with 10 named test cases. No automated unit tests exist.

**Environment:** Tests run against the **live `qbi-rg` resource group**. The Azure Functions backend runs locally (`func start`), connecting to live Azure Storage and Service Bus via `local.settings.json`. There is no separate test/staging environment.

**Test isolation:** Queue is purged (delete + recreate) between scenarios using `az servicebus` CLI.

**Verification method:** Each scenario queries Azure Table Storage directly using `az storage entity query` and Python inline scripts to check expected `TrendLabel`, `SlaStatus`, `AlertSeverity`, `RootCause` values.

### Test Scenarios
| ID | Name | What it validates |
|---|---|---|
| TC-01 | Empty Queue Baseline | No messages → Idle, OK, None |
| TC-02 | Consumer Stopped | 20 messages, no consumer → Critical within 2 min |
| TC-03 | Growing Backlog | Escalating severity as messages accumulate |
| TC-04 | Stable Balanced Queue | Balanced in/out → no false alarms |
| TC-05 | Auto Recovery After Fix | Alert clears itself after consumer resumes |
| TC-06 | Slow Drain + SLA Breach | Draining but still breaching SLA |
| TC-07 | DLQ Growth | Messages expiring → DLQGrowth root cause |
| TC-08 | Idle Stale Messages | 2 stale messages → Idle, NOT ConsumerStopped |
| TC-09 | Burst Arrival on Empty Queue | Sudden spike not mistaken for consumer crash |
| TC-10 | Full Lifecycle | OK → Critical → Recovering → OK |

---

## 15. Running Locally

### Prerequisites
- .NET 8 SDK
- Azure Functions Core Tools v4 (`npm install -g azure-functions-core-tools@4`)
- Node.js 18+
- Azure CLI (for test scenarios)
- `backend/local.settings.json` (not in repo — contains live connection strings)

### Start both services
```bash
./start.sh
# Backend:  http://localhost:7071/api
# Frontend: http://localhost:3000
```

Or separately:
```bash
# Backend
cd backend && dotnet build && func start

# Frontend
cd frontend && npm start     # port 3000
# or
cd frontend && npm run dev   # port 5173
```

### Frontend environment
`VITE_API_URL` environment variable — if not set, defaults to `http://localhost:7071/api`.

---

## 16. Known Issues / Future Work

- `Settings.jsx` uses `window.location.reload()` after mutations instead of `queuesR.refetch()` — a leftover that could be cleaned up
- `emailRecipients` field is stored but email sending is not implemented (only Teams webhook works)
- `StatusCard.jsx` exists in the repo but is not used — replaced by `StatusRow.jsx`
- No automated tests — all testing is manual injection scenarios
- Single environment — no staging/dev separation from production Azure resources

# API Reference

Base URL:
- **Local:** `http://localhost:7071/api`
- **Azure (live):** `https://qbi-function-app-ardvf6ffahaucwc0.centralus-01.azurewebsites.net/api`

All endpoints return `application/json`.

## Authentication

All HTTP trigger bindings use `AuthorizationLevel.Anonymous` in code — this does **not** mean the API is public. Authentication is enforced at the infrastructure layer:

| Environment | Auth mechanism |
|---|---|
| **Azure (production)** | Azure AD Easy Auth — the Functions host validates the Bearer token before the request reaches function code. Unauthenticated requests receive `HTTP 401`. The frontend acquires tokens via MSAL React and sends them as `Authorization: Bearer <token>`. |
| **Local development** | No auth — Easy Auth is an Azure-only infrastructure feature. Endpoints are open on `localhost:7071`. Set `VITE_AUTH_ENABLED=false` in `frontend/.env.local` to bypass the MSAL login gate in the frontend. |

## CORS

CORS is handled in two places:

- **`backend/host.json`** — declares allowed origins, headers, and credential support for the Functions host
- **Application Settings in Azure** — the Static Web App origin (`https://nice-dune-05f7a3710.7.azurestaticapps.net`) is added via `az functionapp cors add`
- **In-code OPTIONS handler** — `DashboardFunction` includes an `OPTIONS {*route}` handler that returns `HTTP 200` for all preflight requests, required because the Functions host does not automatically handle preflight for routes with path parameters

Allowed headers: `Content-Type`, `Authorization`, `x-functions-key`

---

## Endpoints (10 total)

| Method | Route | Description |
|---|---|---|
| GET | `/api/queues` | All monitored queues with current status |
| GET | `/api/queues/{name}/status` | Latest computed status for one queue |
| GET | `/api/queues/{name}/history` | Time-series status history |
| GET | `/api/queues/{name}/snapshots` | Raw collector snapshots |
| GET | `/api/queues/{name}/alerts` | Incident history |
| POST | `/api/queues` | Create a new monitored queue configuration |
| PUT | `/api/queues/{name}` | Update an existing queue configuration |
| DELETE | `/api/queues/{name}` | Remove a queue configuration |
| GET | `/api/health` | System health check |
| OPTIONS | `/{*route}` | CORS preflight handler (internal) |

---

## GET `/api/queues`

Returns all monitored queues with their current status.

### Response

```json
[
  {
    "queueName": "qbi-queue",
    "isEnabled": true,
    "slaMinutes": 3,
    "activeCount": 42,
    "waitTimeMinutes": 1.4,
    "slaStatus": "BREACHING",
    "trendLabel": "Growing",
    "rootCause": "ConsumerSlowdown",
    "alertSeverity": "Warning",
    "gapPerMin": -3.2,
    "acceleration": 0.5,
    "dlqCount": 0,
    "incomingPerMin": 5.1,
    "outgoingPerMin": 1.9,
    "lastUpdatedUtc": "2026-06-11T19:38:30Z"
  }
]
```

---

## GET `/api/queues/{name}/status`

Returns the latest computed status for a single queue.

### Path Parameters

| Parameter | Description |
|---|---|
| `name` | Queue name (matches `RowKey` in `QueueConfig` table) |

### Response

Same shape as a single item from `GET /api/queues`.

### Field Reference

| Field | Type | Description |
|---|---|---|
| `activeCount` | int | Current messages in queue |
| `waitTimeMinutes` | double | Estimated wait: `activeCount / outgoingPerMin` |
| `slaStatus` | string | `OK` / `BREACHING` / `UNKNOWN` |
| `trendLabel` | string | `Idle` / `Growing` / `GrowingFast` / `Draining` / `DrainingFast` / `Stable` |
| `rootCause` | string | See root cause values below |
| `alertSeverity` | string | `None` / `Warning` / `Critical` |
| `gapPerMin` | double | `outgoingPerMin - incomingPerMin` (negative = backlog growing) |
| `acceleration` | double | Rate of change of the active count delta |

### Root Cause Values

| Value | Meaning |
|---|---|
| `Healthy` | Queue operating normally |
| `ConsumerStopped` | Outgoing rate dropped to zero with non-zero backlog |
| `ConsumerSlowdown` | Consumer processing slower than incoming rate |
| `ProducerSpike` | Incoming rate spiked above normal, consumer keeping up |
| `ProducerSpikeAndConsumerSlowdown` | Both conditions simultaneously |
| `DLQGrowth` | Dead-letter queue growing, regardless of active count |
| `Recovering` | Previously breached, now actively draining |
| `Unknown` | Insufficient snapshot history to classify |

---

## GET `/api/queues/{name}/history`

Returns time-series status history for charting and analysis.

### Query Parameters

| Parameter | Type | Description |
|---|---|---|
| `minutes` | int | Last N minutes of history (default: 30) |
| `from` | ISO 8601 | Start of custom range (e.g. `2026-05-19T00:00:00Z`) |
| `to` | ISO 8601 | End of custom range (e.g. `2026-05-19T23:59:59Z`) |

Use either `minutes` or `from`/`to` — not both.

### Response

```json
[
  {
    "timestampUtc": "2026-05-19T21:10:00Z",
    "activeCount": 187,
    "waitTimeMinutes": 6.2,
    "slaStatus": "BREACHING",
    "alertSeverity": "Critical",
    "trendLabel": "Growing",
    "rootCause": "ConsumerStopped",
    "gapPerMin": -12.4,
    "incomingPerMin": 12.4,
    "outgoingPerMin": 0
  }
]
```

Results are ordered oldest-first, suitable for direct use in chart data arrays.

---

## GET `/api/queues/{name}/snapshots`

Returns raw metric snapshots written by the CollectorFunction. Useful for inspecting the raw data before the Analyzer pipeline processes it.

### Query Parameters

Same as `/history` — `minutes`, `from`, `to`.

### Response

```json
[
  {
    "timestampUtc": "2026-05-19T21:10:00Z",
    "activeCount": 187,
    "incomingPerMin": 12.4,
    "outgoingPerMin": 0.0,
    "dlqCount": 0
  }
]
```

Results are ordered oldest-first.

---

## GET `/api/queues/{name}/alerts`

Returns incident history — one record per incident (not per alert sent).

### Response

```json
[
  {
    "incidentId": "20d7060c-8a5d-4970-bf8d-02a503630625",
    "openedAtUtc": "2026-05-19T21:12:46Z",
    "resolvedAtUtc": "2026-05-19T21:24:46Z",
    "status": "Resolved",
    "peakSeverity": "Critical",
    "firstRootCause": "ConsumerStopped",
    "alertCount": 3,
    "durationMinutes": 11
  }
]
```

### Field Reference

| Field | Type | Description |
|---|---|---|
| `incidentId` | GUID | Unique identifier for this incident |
| `status` | string | `Open` / `Resolved` |
| `peakSeverity` | string | Highest severity reached during incident |
| `firstRootCause` | string | Root cause at incident open time (may be `Unknown` on cold start) |
| `alertCount` | int | Total notifications dispatched — initial + cooldown-window reminders |
| `durationMinutes` | int | Minutes from open to resolved; current elapsed time if still open |

---

## POST `/api/queues`

Creates a new monitored queue configuration.

### Request Body

```json
{
  "queueName": "my-queue",
  "namespace": "my-sb-ns.servicebus.windows.net",
  "subscriptionId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "resourceGroupName": "my-rg",
  "slaMinutes": 5,
  "isEnabled": true,
  "cooldownMinutes": 10,
  "warningThreshold": 0.6,
  "criticalThreshold": 1.0,
  "teamsWebhookUrl": "https://...",
  "emailRecipients": "ops@example.com,oncall@example.com"
}
```

### Request Body Fields

| Field | Type | Required | Description |
|---|---|---|---|
| `queueName` | string | Yes | Must be unique — used as the storage RowKey |
| `namespace` | string | Yes | Service Bus namespace hostname |
| `subscriptionId` | string | Yes | Azure subscription ID for Monitor API access |
| `resourceGroupName` | string | Yes | Resource group containing the Service Bus namespace |
| `slaMinutes` | int | Yes | SLA breach threshold in minutes |
| `isEnabled` | bool | Yes | Whether QBIS monitors this queue |
| `cooldownMinutes` | int | Yes | Minimum minutes between reminder alerts |
| `warningThreshold` | double | Yes | Fraction of SLA at which Warning fires (e.g. `0.6`) |
| `criticalThreshold` | double | Yes | Fraction of SLA at which Critical fires (e.g. `1.0`) |
| `teamsWebhookUrl` | string | No | Teams Incoming Webhook or Workflows connector URL |
| `emailRecipients` | string | No | Comma-separated SMTP recipients |

### Responses

| Status | Body | Condition |
|---|---|---|
| 200 OK | `{"message": "Queue 'my-queue' created"}` | Success |
| 400 Bad Request | `{"error": "QueueName is required"}` | Missing required field |
| 400 Bad Request | `{"error": "Invalid JSON body"}` | Malformed request |
| 409 Conflict | `{"error": "Queue 'my-queue' already exists"}` | Duplicate queue name |

---

## PUT `/api/queues/{name}`

Updates an existing queue configuration. All fields in the request body overwrite the stored values.

### Path Parameters

| Parameter | Description |
|---|---|
| `name` | Queue name to update |

### Request Body

Same shape as `POST /api/queues`.

### Responses

| Status | Body | Condition |
|---|---|---|
| 200 OK | `{"message": "Queue 'my-queue' updated"}` | Success |
| 400 Bad Request | `{"error": "Request body required"}` | Empty body |
| 400 Bad Request | `{"error": "Invalid JSON body"}` | Malformed request |
| 404 Not Found | `{"error": "Queue 'my-queue' not found"}` | Queue does not exist |

---

## DELETE `/api/queues/{name}`

Removes a queue configuration. The Collector and Analyzer will stop processing this queue on the next cycle.

### Path Parameters

| Parameter | Description |
|---|---|
| `name` | Queue name to delete |

### Responses

| Status | Body | Condition |
|---|---|---|
| 200 OK | `{"message": "Queue 'my-queue' deleted"}` | Success |
| 404 Not Found | `{"error": "Queue 'my-queue' not found"}` | Queue does not exist |

---

## GET `/api/health`

Returns system health — confirms the Functions host and storage are reachable.

### Response

```json
{
  "status": "Healthy",
  "timestamp": "2026-06-11T19:38:30Z",
  "lastCollectorRun": "2026-06-11T19:38:00Z",
  "lastAnalyzerRun": "2026-06-11T19:38:30Z"
}
```

`status` is `"Degraded"` if the last collector or analyzer run is more than 3 minutes ago.

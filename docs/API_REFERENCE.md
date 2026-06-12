# API Reference

Base URL: `http://localhost:7071/api` (local) or your Azure Functions URL (production)

All endpoints return `application/json`. No authentication required (secure via network/APIM in production).

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
    "slaStatus": "Warning",
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

```json
{
  "queueName": "qbi-queue",
  "isEnabled": true,
  "slaMinutes": 3,
  "activeCount": 42,
  "waitTimeMinutes": 1.4,
  "slaStatus": "Warning",
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
```

### Field Reference

| Field | Type | Description |
|---|---|---|
| `activeCount` | int | Current messages in queue |
| `waitTimeMinutes` | double | Estimated wait: `activeCount / outgoingPerMin` |
| `slaStatus` | string | `OK` / `Warning` / `Critical` |
| `trendLabel` | string | `Idle` / `Growing` / `GrowingFast` / `Draining` / `Stable` / `Recovering` |
| `rootCause` | string | See root cause values below |
| `alertSeverity` | string | `None` / `Info` / `Warning` / `Critical` |
| `gapPerMin` | double | `outgoingPerMin - incomingPerMin` (negative = backlog growing) |
| `acceleration` | double | Rate of change of the active count delta |

### Root Cause Values

| Value | Meaning |
|---|---|
| `Healthy` | Queue operating normally |
| `ConsumerStopped` | Outgoing rate dropped to zero |
| `ConsumerSlowdown` | Consumer processing slower than incoming rate |
| `ProducerSpike` | Incoming rate spiked above normal |
| `ProducerSpikeAndConsumerSlowdown` | Both conditions simultaneously |
| `DLQGrowth` | Dead-letter queue growing |
| `Recovering` | Previously breached, now draining |
| `Unknown` | Insufficient data to classify |

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
    "slaStatus": "Critical",
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
  },
  {
    "incidentId": "4a0a01a8-754c-46a1-afb2-d98e1963e363",
    "openedAtUtc": "2026-05-19T01:40:46Z",
    "resolvedAtUtc": null,
    "status": "Open",
    "peakSeverity": "Critical",
    "firstRootCause": "ConsumerStopped",
    "alertCount": 21,
    "durationMinutes": 1157
  }
]
```

### Field Reference

| Field | Type | Description |
|---|---|---|
| `incidentId` | GUID | Unique identifier for this incident |
| `status` | string | `Open` / `Resolved` |
| `peakSeverity` | string | Highest severity reached during incident |
| `firstRootCause` | string | Root cause at incident start |
| `alertCount` | int | Total Teams notifications sent |
| `durationMinutes` | int | Minutes from open to resolved (or current if still open) |

---

## GET `/api/health`

Returns system health — confirms the Functions host and storage are reachable.

### Response

```json
{
  "status": "Healthy",
  "timestamp": "2026-06-11T19:38:30Z"
}
```

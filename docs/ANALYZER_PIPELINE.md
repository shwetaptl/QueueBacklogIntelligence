# Analyzer Intelligence Pipeline

The `AnalyzerService` runs every 30 seconds and transforms raw queue snapshots into actionable intelligence through a 9-step pipeline.

---

## Pipeline Overview

```
Raw Snapshots (current + previous)
        │
        ▼
Step 1  Compute deltas
        │
        ▼
Step 2  Apply noise floor
        │
        ▼
Step 3  Classify queue state
        │
        ▼
Step 4  Derive message rates
        │
        ▼
Step 5  Calculate wait time vs SLA
        │
        ▼
Step 6  Assign trend label
        │
        ▼
Step 7  Classify root cause
        │
        ▼
Step 8  Determine alert severity
        │
        ▼
Step 9  Decide whether to alert
        │
        ▼
QueueStatus row written to Table Storage
```

---

## Step 1 — Compute Deltas

Subtracts previous snapshot values from current:

```
deltaActive   = current.ActiveCount   - previous.ActiveCount
deltaIncoming = current.IncomingPerMin - previous.IncomingPerMin
deltaOutgoing = current.OutgoingPerMin - previous.OutgoingPerMin
```

If there is no previous snapshot (first run), all deltas are zero.

---

## Step 2 — Apply Noise Floor

Small fluctuations in an otherwise stable queue should not trigger state changes. A noise floor filters out insignificant deltas:

- `deltaActive` is zeroed if `|deltaActive| < noiseFloor`
- Default noise floor: **2 messages**

This prevents a queue sitting at 0–1 messages from constantly toggling between Growing and Draining.

---

## Step 3 — Classify Queue State

| State | Condition |
|---|---|
| `Idle` | `activeCount == 0` and `deltaActive == 0` |
| `Growing` | `deltaActive > 0` |
| `Draining` | `deltaActive < 0` |
| `Stable` | `deltaActive == 0` and `activeCount > 0` |

---

## Step 4 — Derive Message Rates

If Azure Monitor rates are available (IncomingPerMin, OutgoingPerMin):
- Use them directly

If Azure Monitor is unavailable (credentials missing, 1–2 min lag):
- Estimate from delta: `estimatedRate = |deltaActive| / elapsedMinutes`

Rates are used in wait time calculation and root cause classification.

---

## Step 5 — Calculate Wait Time vs SLA

```
waitTimeMinutes = activeCount / outgoingPerMin
slaStatus       = waitTimeMinutes / slaMinutes
```

| slaStatus | Meaning |
|---|---|
| < 0.60 | OK |
| 0.60 – 1.0 | Warning |
| > 1.0 | Critical (breached) |

**Predictive warning:** If `state == Growing` and `waitTimeMinutes` is rising and projected to breach within one SLA duration, severity is raised to Warning even before threshold is hit.

---

## Step 6 — Assign Trend Label

Human-readable label for the dashboard:

| Label | Condition |
|---|---|
| `Idle` | State is Idle |
| `Growing` | State is Growing, below warning threshold |
| `GrowingFast` | State is Growing, above warning threshold |
| `Draining` | State is Draining |
| `Stable` | State is Stable |
| `Recovering` | Was breached, now draining |

---

## Step 7 — Classify Root Cause

The root cause tells engineers *why* the queue is behaving as it is:

| Root Cause | Detection Logic |
|---|---|
| `Healthy` | Queue is idle or draining normally |
| `ConsumerStopped` | Growing + outgoingPerMin ≈ 0 |
| `ConsumerSlowdown` | Growing + outgoingPerMin > 0 but incomingPerMin > outgoingPerMin |
| `ProducerSpike` | Growing + incomingPerMin significantly above baseline |
| `ProducerSpikeAndConsumerSlowdown` | Both producer spike and consumer slowdown detected |
| `DLQGrowth` | DLQ count growing regardless of active count |
| `Recovering` | Was Critical/Warning, now draining back toward healthy |
| `Unknown` | Insufficient data to classify |

---

## Step 8 — Determine Alert Severity

```
if waitTimeMinutes >= slaMinutes * criticalThreshold → Critical
if waitTimeMinutes >= slaMinutes * warningThreshold  → Warning
if predictedBreach within 1 SLA duration            → Warning
else                                                 → None
```

### Sticky De-escalation

Once severity reaches Warning or Critical, it does **not** drop back to None on a single OK reading. The system requires **2 consecutive OK readings** before de-escalating. This prevents alert flapping when a consumer briefly catches up then falls behind again.

---

## Step 9 — Decide Whether to Alert

The AlertDispatcherFunction reads the latest QueueStatus and the latest AlertRecord to decide what action to take:

| Condition | Action |
|---|---|
| Severity ≥ Warning, no open incident | **New** alert — open incident |
| Severity escalated (Warning → Critical) | **Escalation** alert |
| Severity still elevated, reminder interval passed | **Reminder** alert |
| Severity returned to None, incident was open | **Recovery** alert — close incident |
| Severity elevated but reminder interval not reached | **Suppress** — no alert |
| Severity None, no open incident | **Suppress** — no alert |

Reminder interval is configurable via `CooldownMinutes` in `QueueConfig` (default: 5 minutes).

---

## Constants

| Constant | Value | Purpose |
|---|---|---|
| Noise floor | 2 messages | Minimum delta to register a change |
| Warning threshold | 0.60 | 60% of SLA triggers warning |
| Critical threshold | 1.0 | 100% of SLA triggers critical |
| Sticky OK count | 2 | Consecutive OKs required to de-escalate |
| Default cooldown | 5 min | Minimum time between reminder alerts |
| Predictive window | 1× SLA | How far ahead to project breach |

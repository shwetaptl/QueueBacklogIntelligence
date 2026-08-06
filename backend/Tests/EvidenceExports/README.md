# Evidence Exports

Machine-readable exports of Azure Table Storage data from live QBIS test sessions.
Exported from storage account `queuebacklogsa`, table `QueueStatus` and `AlertRecord`.

## Files

| File | Rows | Scenario | FR Coverage |
|---|---|---|---|
| `QueueStatus_TC01_baseline_healthy.json` | 10 | TC-01 Empty queue — sustained Healthy/Idle | FR-2.2.1 |
| `QueueStatus_TC02_consumer_stopped.json` | 3 | TC-02 Consumer stopped — Critical/BREACHING | FR-2.2.1, FR-3.1.1 |
| `QueueStatus_TC05_recovery.json` | 5+ | TC-05 Recovery — Recovering → Healthy transition | FR-2.2.1, FR-3.1.1 |
| `QueueStatus_DLQGrowth.json` | 3 | DLQ growth detected with near-zero ActiveCount | FR-2.2.1 |
| `AlertRecord_all_incidents.json` | 7 | Full alert lifecycle records — open/resolved incidents | FR-3.1.1, FR-5.3.1 |

## Schema Reference

### QueueStatus row fields
- `PartitionKey` — queue name
- `RowKey` — reverse-timestamp (most recent row sorts first)
- `TimestampUtc` — when the Analyzer wrote this row
- `ActiveCount` — messages in the queue at collection time
- `DLQCount` — dead-letter queue message count
- `IncomingPerMin` / `OutgoingPerMin` / `GapPerMin` — smoothed rate values (3-snapshot average)
- `Acceleration` — rate-of-change of GapPerMin
- `RootCause` — classifier output: Healthy | ConsumerStopped | ProducerSpike | ConsumerSlowdown | DLQGrowth | Recovering | Unknown
- `TrendLabel` — Idle | Stable | Growing | GrowingFast | Draining | DrainingFast
- `SlaStatus` — OK | AT_RISK | BREACHING
- `WaitTimeMinutes` — estimated time to SLA breach at current outgoing rate
- `AlertSeverity` — None | Warning | Critical
- `NeedToSendAlert` — true on the row that triggered an alert dispatch

### AlertRecord row fields
- `IncidentId` — UUID, also used as RowKey
- `OpenedAtUtc` — timestamp of the first alert for this incident
- `ResolvedAtUtc` — timestamp when 2 consecutive OK readings cleared the incident
- `Status` — Open | Resolved
- `PeakSeverity` — highest severity reached during the incident
- `AlertCount` — total alerts sent (initial + reminders; deduplication prevents duplicates)
- `FirstRootCause` — root cause classification at incident open time

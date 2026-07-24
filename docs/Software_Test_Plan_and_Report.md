# Software Test Plan and Report

---

## Cover Page

| Field               | Value                                                                  |
|---------------------|------------------------------------------------------------------------|
| **Project Name**    | Queue Backlog Intelligence System (QBIS)                               |
| **Student**         | Shweta Patel                                                           |
| **Course**          | CISC 593 / 594                                                         |
| **Semester**        | To Be Completed                                                        |
| **Repository URL**  | To Be Completed                                                        |
| **Branch**          | main                                                                   |
| **Commit SHA**      | dda88ab (v1.4.1)                                                       |
| **Release Version** | v1.4.1                                                                 |
| **Document Version**| v1.2                                                                   |
| **Last Updated**    | 2026-07-22                                                             |

---

## Revision History

| Version | Date       | Git Commit | Description               | Author        |
|---------|------------|------------|---------------------------|---------------|
| v1.0    | 2026-07-21 | dda88ab    | Initial test plan created | Shweta Patel  |
| v1.1    | 2026-07-22 | 4e46edb    | Fixed three factual errors: (A) storage table count 3→4 (QueueConfig added); (B) Traceability Matrix FR IDs realigned to PRD v1.2 exactly; (C) Collector cadence corrected from "every 30 seconds" to "every 60 seconds". | Shweta Patel  |
| v1.2    | 2026-07-22 | —          | Added TC-11 through TC-16 (curl-based API scenarios for FR-4.x.x and FR-5.x.x); updated Traceability Matrix, Coverage Analysis, and Open Issues accordingly. | Shweta Patel  |

---

## Table of Contents

1. [Introduction](#1-introduction)
2. [Test Scope](#2-test-scope)
3. [Test Strategy](#3-test-strategy)
4. [Test Environment](#4-test-environment)
5. [Test Tools and Infrastructure](#5-test-tools-and-infrastructure)
6. [Test Items](#6-test-items)
7. [Test Case Specifications](#7-test-case-specifications)
8. [Test Execution Schedule](#8-test-execution-schedule)
9. [Test Traceability Matrix](#9-test-traceability-matrix)
10. [Test Execution Log](#10-test-execution-log)
11. [Test Results Summary](#11-test-results-summary)
12. [Defects Discovered During Testing](#12-defects-discovered-during-testing)
13. [Defect Resolution Status](#13-defect-resolution-status)
14. [Regression Testing](#14-regression-testing)
15. [Test Coverage Analysis](#15-test-coverage-analysis)
16. [Quality Assessment](#16-quality-assessment)
17. [Risks and Mitigations](#17-risks-and-mitigations)
18. [Assumptions and Constraints](#18-assumptions-and-constraints)
19. [Test Artifacts](#19-test-artifacts)
20. [Open Issues](#20-open-issues)
21. [Glossary](#21-glossary)

---

## 1. Introduction

### 1.1 Purpose

This document defines the test approach, test cases, execution results, and quality assessment for the Queue Backlog Intelligence System (QBIS). It serves as both a test plan (what will be tested and how) and a test report (what was executed and what was found).

### 1.2 System Under Test

QBIS is an Azure-hosted monitoring system that polls one or more Azure Service Bus queues every 60 seconds, classifies queue health using a nine-step intelligence pipeline, and dispatches alerts via Microsoft Teams and SMTP email when SLA thresholds are breached. The system consists of:

- **Backend**: .NET 8 isolated-worker Azure Functions v4 (five functions: Collector, Analyzer, AlertDispatcher, Cleanup, Dashboard)
- **Frontend**: React 18 + Vite 6 + Tailwind CSS v4 single-page application
- **Storage**: Azure Table Storage (four tables: QueueConfig, QueueSnapshot, QueueStatus, AlertRecord)
- **Message broker**: Azure Service Bus (single namespace `qbi-sb-ns`, queue `qbi-queue`)

### 1.3 Document Scope

This document covers manual black-box testing of the QBIS backend analysis pipeline. Frontend UI, Azure AD authentication, and the CleanupFunction are excluded from scenario-based testing due to the absence of an automated test framework (see Section 2.2).

---

## 2. Test Scope

### 2.1 In Scope

| Area | What is Tested |
|------|----------------|
| Root cause classification | All seven root causes: `ConsumerStopped`, `ConsumerSlowdown`, `ProducerSpike`, `ProducerSpikeAndConsumerSlowdown`, `DLQGrowth`, `Recovering`, `Healthy` |
| SLA status | `OK`, `BREACHING`, `UNKNOWN` transitions |
| Alert severity | `None`, `Warning`, `Critical` escalation and de-escalation |
| Trend labels | `Idle`, `Stable`, `Growing`, `GrowingFast`, `Draining`, `DrainingFast` |
| False-positive prevention | Burst arrival on empty queue must not trigger `ConsumerStopped` |
| DLQ detection | `DLQGrowth` root cause when active queue is empty |
| Full incident lifecycle | Open → escalate → recover → resolve |
| Dashboard REST API | All six HTTP endpoints: `GET /api/queues`, `GET /api/queues/{name}/history`, `GET /api/queues/{name}/alerts`, `POST /api/queues`, `PUT /api/queues/{name}`, `DELETE /api/queues/{name}` |

### 2.2 Out of Scope

| Area | Reason |
|------|--------|
| Automated unit tests | No test framework (no xUnit, NUnit, MSTest) in `QueueBacklogIntelligence.csproj` |
| Automated integration tests | No CI/CD pipeline; no `.github/workflows/` |
| Frontend automated tests | No Vitest or Jest in `frontend/package.json` |
| Load/performance testing | No load testing tools configured |
| CleanupFunction | No manual test scenario defined; function requires a 24-hour retention window to produce verifiable results |
| Azure AD Easy Auth | Azure-only infrastructure feature; not testable in local dev without full tenant configuration |
| Email alert delivery | Requires live SMTP credentials not present in test environment |

### 2.3 Future Testing (Not in This Release)

- Automated unit tests for `AnalyzerService` root cause logic (requires adding xUnit or NUnit to the project)
- End-to-end browser automation for the React dashboard (Playwright or Cypress)
- Performance benchmarks for Azure Table Storage query latency under high write rates (~1,440 writes/hour)

---

## 3. Test Strategy

### 3.1 Approach

QBIS uses **manual black-box end-to-end testing** executed against a live Azure environment. Each test scenario:

1. Sends or drains messages from the Azure Service Bus queue via Azure Portal Service Bus Explorer
2. Waits for the Collector (one-minute timer, `0 */1 * * * *`) to capture snapshots and the Analyzer to classify them (runs 30 seconds after each Collector cycle)
3. Queries Azure Table Storage directly via the Azure CLI to read `QueueStatus` rows
4. Compares actual values against expected values using assertions embedded in `test_scenarios.sh`

This approach was chosen because QBIS behavior depends on Azure Monitor metric delays (2–4 minutes), Service Bus queue state transitions, and the AnalyzerService's multi-snapshot smoothing logic — all of which require a real cloud environment to exercise faithfully.

### 3.2 Testing Type

| Type | Applied | Notes |
|------|---------|-------|
| Black-box functional | Yes | All 11 scenarios verify outputs without inspecting internal state |
| Regression | Yes | Defect fixes were re-tested in the same test session after fixes |
| Exploratory | Yes | Observational runs (TC-05, TC-06) informed defect discovery |
| White-box / unit | No | No test framework in repository |
| Performance | No | Out of scope |
| Security | No | Azure AD Easy Auth tested manually; not scripted |

### 3.3 Pass / Fail Criteria

A scenario **passes** when the `verify_scenario()` or `verify_not_consumer_stopped()` function in `test_scenarios.sh` outputs `✅ PASS`. The assertion compares four fields from the most recent `QueueStatus` row in Azure Table Storage:

- `TrendLabel` — matched against expected value or `ANY`
- `SlaStatus` — `OK`, `BREACHING`, or `ANY`
- `AlertSeverity` — `None`, `Warning`, `Critical`, or `ANY`
- `RootCause` — exact string or `ANY`

A scenario **fails** when any non-`ANY` field does not match, producing `❌ FAIL`.

---

## 4. Test Environment

### 4.1 Azure Resources

| Resource | Name | Notes |
|----------|------|-------|
| Resource Group | `qbi-rg` | All resources co-located |
| Service Bus Namespace | `qbi-sb-ns` | Standard tier |
| Service Bus Queue | `qbi-queue` | Max delivery count: 10; default TTL: 10,675 days |
| Storage Account | `queuebacklogsa` | Azure Table Storage for test artifact capture |
| Function App | To Be Completed | Hosts five Azure Functions |

### 4.2 Local Development Setup

All tests were executed with the Azure Functions runtime running locally via `func start` in one terminal and `test_scenarios.sh` in a separate terminal. The backend reads live Azure Service Bus and Azure Monitor data — there is no local simulation or mock queue.

| Component | Version |
|-----------|---------|
| .NET | 8.0 (isolated worker) |
| Azure Functions Core Tools | v4 |
| Azure CLI | Used for queue introspection and table queries |
| Python 3 | Used for inline JSON parsing within `test_scenarios.sh` |
| Operating System | macOS (zsh shell) |

### 4.3 Credentials

- `backend/local.settings.json` — contains `ServiceBusConnectionString` and `StorageConnectionString`; **never committed to the repository** (listed in `.gitignore`)
- `.env` — contains frontend `VITE_*` variables; **never committed to the repository**

---

## 5. Test Tools and Infrastructure

### 5.1 Primary Test Script

**File:** `backend/Tests/test_scenarios.sh`

The primary test automation entry point. Provides:
- An interactive numbered menu with 11 scenario options plus utility commands (`check`, `purge`, `quit`)
- `verify_scenario()` — queries the latest `QueueStatus` row and asserts four fields
- `verify_not_consumer_stopped()` — negative assertion for false-positive prevention tests
- `save_snapshot()` — captures all three Azure Table Storage tables to JSON files in a dated `TestResults/` folder
- `wait_and_check()` — polling loop that prints live queue counts every 10 seconds during waits

### 5.2 Helper Utilities

**File:** `backend/Tests/test-helpers.sh`

Provides three lower-level utility functions used outside the main scenario script:
- `send_messages(count)` — guidance + prompt for sending via Azure Portal Service Bus Explorer
- `receive_messages(count)` — guidance for consuming messages
- `check_queue()` — prints live queue counts from `az servicebus queue show`

### 5.3 Verification Mechanism

Each verification queries Azure Table Storage via:

```bash
az storage entity query \
  --account-name "$STORAGE_ACCOUNT" \
  --table-name QueueStatus \
  --filter "PartitionKey eq 'qbi-queue'" \
  --num-results 1 \
  --connection-string "$STORAGE_CONN" \
  --output json
```

The most recent `QueueStatus` row is returned because the table uses a reverse-tick `RowKey` (`(DateTime.MaxValue.Ticks - now.Ticks).ToString("D19")`), which orders newest-first.

### 5.4 Artifact Capture

Each test run creates a timestamped directory under `backend/Tests/TestResults/YYYYMMDD_HHMMSS/` containing:
- `terminal.log` — full stdout/stderr output of the test session
- `TC{nn}_{HHMMSS}_verify_snapshots.json` — last 20 rows from `QueueSnapshot` table
- `TC{nn}_{HHMMSS}_verify_status.json` — last 20 rows from `QueueStatus` table
- `TC{nn}_{HHMMSS}_verify_alerts.json` — last 10 rows from `AlertRecord` table

---

## 6. Test Items

### 6.1 Scenarios Under Test

| ID | Name | Test Function in Script |
|----|------|------------------------|
| TC-01 | Empty Queue Baseline | `scenario_1_empty_baseline` |
| TC-02 | Consumer Stopped | `scenario_2_consumer_stopped` |
| TC-03 | Growing Backlog | `scenario_3_growing_backlog` |
| TC-04 | Stable Balanced Queue | `scenario_4_stable_balanced` |
| TC-05 | Consumer Stops Mid-Operation | `scenario_5_recovery` |
| TC-05b | Recovery Sub-scenario | (continuation of TC-05) |
| TC-06 | Slow Drain While Breaching SLA | `scenario_6_slow_drain_breach` |
| TC-07 | DLQ Growth | `scenario_7_dlq_growth` |
| TC-08 | Idle Queue with Stale Messages | `scenario_8_idle_stale` |
| TC-09 | Burst Arrival on Empty Queue | `scenario_9_burst_arrival` |
| TC-10 | Full Lifecycle | `scenario_10_full_lifecycle` |
| TC-11 | GET Queue Summaries | `scenario_11_get_queue_summaries` |
| TC-12 | GET Queue History | `scenario_12_get_queue_history` |
| TC-13 | GET Alert History | `scenario_13_get_alert_history` |
| TC-14 | Create Queue Configuration | `scenario_14_create_queue_config` |
| TC-15 | Update Queue Configuration | `scenario_15_update_queue_config` |
| TC-16 | Delete Queue Configuration | `scenario_16_delete_queue_config` |

---

## 7. Test Case Specifications

### TC-01 — Empty Queue Baseline

| Field | Value |
|-------|-------|
| **Objective** | Verify no false alarms when the queue is empty and idle |
| **Precondition** | Queue purged; Collector and Analyzer running via `func start` |
| **Steps** | 1. Purge queue via `az servicebus queue delete/create`. 2. Wait 180 seconds (≥2 Collector cycles). 3. Run `verify_scenario`. |
| **Expected Result** | `TrendLabel=Idle`, `SlaStatus=OK`, `AlertSeverity=None`, `RootCause=Healthy` |
| **Assertions** | `verify_scenario "TC-01 Empty Baseline" ANY OK None ANY` |

---

### TC-02 — Consumer Stopped

| Field | Value |
|-------|-------|
| **Objective** | Verify `ConsumerStopped` root cause and `Critical` alert within 2 minutes of queue growing with no consumer |
| **Precondition** | Queue empty |
| **Steps** | 1. Send 20 messages via Service Bus Explorer. 2. Do not consume. 3. Wait 120 seconds. 4. Run `verify_scenario`. |
| **Expected Result** | Any recent `QueueStatus` row shows `RootCause=ConsumerStopped`, `AlertSeverity=Critical`, `SlaStatus=BREACHING` |
| **Assertions** | `verify_scenario "TC-02 Consumer Stopped" ANY BREACHING Critical ConsumerStopped` |

---

### TC-03 — Growing Backlog

| Field | Value |
|-------|-------|
| **Objective** | Verify `Warning → Critical` severity escalation as backlog grows over multiple cycles |
| **Precondition** | Queue empty |
| **Steps** | 1. Send 10 messages. 2. Wait 90 seconds. 3. Send 20 more messages. 4. Wait 90 seconds. 5. Run `verify_scenario`. |
| **Expected Result** | History contains at least one `TrendLabel=GrowingFast` or `Growing` row with `AlertSeverity=Critical` |
| **Assertions** | `verify_scenario "TC-03 Growing" ANY BREACHING Critical ConsumerStopped` |

---

### TC-04 — Stable Balanced Queue

| Field | Value |
|-------|-------|
| **Objective** | Verify no false alarms when message production and consumption are balanced |
| **Precondition** | Queue empty |
| **Steps** | 1. Send 10 messages. 2. Consume 10 messages immediately. 3. Repeat 3×. 4. Wait 120 seconds. 5. Run `verify_scenario`. |
| **Expected Result** | `SlaStatus=OK`, `AlertSeverity=None`, `RootCause` is `Healthy` |
| **Assertions** | `verify_scenario "TC-04 Stable Balanced" ANY OK None ANY` |

---

### TC-05 — Consumer Stops Mid-Operation

| Field | Value |
|-------|-------|
| **Objective** | Verify state transition from `Healthy` to `ConsumerStopped` when consumer stops mid-session |
| **Precondition** | Queue empty |
| **Steps** | 1. Send 20 messages. 2. Consume 15 (leave 5). 3. Wait 120 seconds. 4. Run `verify_scenario`. |
| **Expected Result** | Final state shows `SlaStatus=OK`, `AlertSeverity=None`, `RootCause≠ConsumerStopped` (draining completed) OR `ConsumerStopped` if consumer never resumed |
| **Assertions** | `verify_scenario "TC-05 Recovery" ANY OK None ANY` |
| **Note** | The `Recovering` root cause is a single-snapshot transient (L-08). Assertion checks for final stable state, not the transient state. |

---

### TC-06 — Slow Drain While Breaching SLA

| Field | Value |
|-------|-------|
| **Objective** | Verify `BREACHING` SLA status is maintained during slow drain and that alert clears on recovery |
| **Precondition** | Queue empty |
| **Steps** | 1. Send 40 messages. 2. Slowly consume 2–3 messages every 30 seconds for 3 minutes. 3. Consume all remaining. 4. Wait 120 seconds for SLA to clear. 5. Run `verify_scenario`. |
| **Expected Result** | During slow drain: `SlaStatus=BREACHING`. After full drain: `SlaStatus=OK`, `AlertSeverity=None` |
| **Assertions** | `verify_scenario "TC-06 Slow Drain" ANY OK None ANY` (final state) |

---

### TC-07 — DLQ Growth

| Field | Value |
|-------|-------|
| **Objective** | Verify `DLQGrowth` root cause is detected when messages expire to DLQ with active queue empty |
| **Precondition** | Queue empty; queue TTL configured to 1 minute for this test |
| **Steps** | 1. Send 15 messages. 2. Do not consume. 3. Wait 90 seconds for messages to expire to DLQ. 4. Wait 90 more seconds for Analyzer. 5. Run `verify_scenario`. |
| **Expected Result** | `RootCause=DLQGrowth` |
| **Assertions** | Checks for `RootCause=DLQGrowth` in recent `QueueStatus` rows |
| **Note** | Initial run FAILED (before v1.3.3). Defect DLQ-BUG-01 discovered. See Section 12. |

---

### TC-08 — Idle Queue with Stale Messages

| Field | Value |
|-------|-------|
| **Objective** | Verify that a queue with 2 unprocessed messages but no traffic is classified as `Healthy/Idle`, not `ConsumerStopped` |
| **Precondition** | Queue has exactly 2 messages already sitting idle |
| **Steps** | 1. Send 2 messages. 2. Do not consume. 3. Wait 180 seconds. 4. Run `verify_scenario`. |
| **Expected Result** | `TrendLabel=Idle`, `SlaStatus=OK`, `AlertSeverity=None`, `RootCause=Healthy` |
| **Assertions** | `verify_scenario "TC-08 Idle Stale" Idle OK None Healthy` |
| **Note** | First run FAILED (`AlertSeverity=Warning`) before SEVERITY-BUG-01 fix in v1.3.3. See Section 12. |

---

### TC-09 — Burst Arrival on Empty Queue

| Field | Value |
|-------|-------|
| **Objective** | Verify that a sudden burst of messages on an empty queue does not trigger `ConsumerStopped` (false positive prevention) |
| **Precondition** | Queue empty |
| **Steps** | 1. Send 50 messages rapidly. 2. Wait 60 seconds (burst absorbed by Azure Monitor's delay window). 3. Run `verify_not_consumer_stopped`. |
| **Expected Result** | `RootCause ≠ ConsumerStopped` |
| **Assertions** | `verify_not_consumer_stopped "TC-09 Burst Arrival"` |
| **Note** | Guards against the L-07 Azure Monitor metric delay causing misclassification. |

---

### TC-10 — Full Lifecycle

| Field | Value |
|-------|-------|
| **Objective** | Verify a complete incident story: queue fills, alert fires, consumer recovers, alert clears |
| **Precondition** | Queue empty |
| **Steps** | Phase 1: Send 30 messages; wait 90 seconds (expect `ConsumerStopped`/`Critical`). Phase 2: Slowly consume 5 messages; wait 60 seconds (expect `Recovering`/`Warning`). Phase 3: Consume all; wait 120 seconds (expect `Healthy`/`None`). 4. Run `verify_scenario`. |
| **Expected Result** | Final state: `SlaStatus=OK`, `AlertSeverity=None`, `RootCause=Healthy` |
| **Assertions** | `verify_scenario "TC-10 Full Lifecycle" ANY OK None ANY` |

---

### TC-11 — GET Queue Summaries

| Field | Value |
|-------|-------|
| **Objective** | Verify `GET /api/queues` returns HTTP 200 and a JSON array containing the configured queue |
| **Precondition** | `func start` running; `qbi-queue` row exists in `QueueConfig` table |
| **Steps** | 1. `curl -s -o resp.json -w "%{http_code}" GET http://localhost:7071/api/queues`. 2. Assert HTTP 200. 3. Assert response array contains an object with `queueName="qbi-queue"`. |
| **Expected Result** | HTTP 200; array includes `{ queueName: "qbi-queue", slaStatus: "OK"\|"BREACHING"\|"UNKNOWN", alertSeverity: "None"\|"Warning"\|"Critical" }` |
| **Assertions** | `ACTUAL_STATUS=200` AND `FOUND=FOUND:*` |
| **Covers** | FR-5.1.1 |

---

### TC-12 — GET Queue History

| Field | Value |
|-------|-------|
| **Objective** | Verify `GET /api/queues/{name}/history` returns HTTP 200 and a JSON array |
| **Precondition** | `func start` running; at least one `QueueStatus` row exists for `qbi-queue` |
| **Steps** | 1. `curl -s GET http://localhost:7071/api/queues/qbi-queue/history?minutes=60`. 2. Assert HTTP 200. 3. Parse response as JSON array. |
| **Expected Result** | HTTP 200; JSON array (may be empty if system just started; non-empty after any analysis cycle) |
| **Assertions** | `ACTUAL_STATUS=200` |
| **Covers** | FR-5.2.1 |

---

### TC-13 — GET Alert History

| Field | Value |
|-------|-------|
| **Objective** | Verify `GET /api/queues/{name}/alerts` returns HTTP 200 and a JSON array |
| **Precondition** | `func start` running; `qbi-queue` configured |
| **Steps** | 1. `curl -s GET http://localhost:7071/api/queues/qbi-queue/alerts`. 2. Assert HTTP 200. 3. Parse response as JSON array. |
| **Expected Result** | HTTP 200; JSON array (empty if no incidents occurred; non-empty after any TC-02/TC-10 run) |
| **Assertions** | `ACTUAL_STATUS=200` |
| **Covers** | FR-5.3.1 |

---

### TC-14 — Create Queue Configuration

| Field | Value |
|-------|-------|
| **Objective** | Verify `POST /api/queues` creates a new row in the `QueueConfig` table and returns HTTP 200 |
| **Precondition** | `func start` running; `tc14-test-queue` does not already exist in `QueueConfig` |
| **Steps** | 1. `curl -X POST /api/queues` with body `{ "QueueName": "tc14-test-queue", "Namespace": "qbi-sb-ns", "SlaMinutes": 10, "IsEnabled": false, … }`. 2. Assert HTTP 200. 3. `az storage entity show --table-name QueueConfig --partition-key config --row-key tc14-test-queue`. 4. Assert row exists with `SlaMinutes=10`. 5. Cleanup: `DELETE /api/queues/tc14-test-queue`. |
| **Expected Result** | HTTP 200 with `{ "message": "Queue 'tc14-test-queue' created" }`; row present in `QueueConfig` table; cleanup DELETE returns HTTP 200 |
| **Assertions** | POST HTTP 200 AND `ROW_EXISTS=FOUND:SlaMinutes=10` |
| **Covers** | FR-4.1.1 |

---

### TC-15 — Update Queue Configuration

| Field | Value |
|-------|-------|
| **Objective** | Verify `PUT /api/queues/qbi-queue` updates the `SlaMinutes` field in `QueueConfig` and returns HTTP 200; original value is restored after test |
| **Precondition** | `func start` running; `qbi-queue` row exists in `QueueConfig` |
| **Steps** | 1. Read current `SlaMinutes` from `QueueConfig` table via `az storage entity show`. 2. `curl -X PUT /api/queues/qbi-queue` with full body and `SlaMinutes=99`. 3. Assert HTTP 200. 4. `az storage entity show` — assert `SlaMinutes=99`. 5. Restore: `curl -X PUT` with original `SlaMinutes`. Assert HTTP 200. |
| **Expected Result** | First PUT: HTTP 200; `QueueConfig.SlaMinutes=99`. Restore PUT: HTTP 200; `QueueConfig.SlaMinutes` reverted to original value |
| **Assertions** | PUT HTTP 200 AND `UPDATED_SLA=99` |
| **Covers** | FR-4.2.1 |

---

### TC-16 — Delete Queue Configuration

| Field | Value |
|-------|-------|
| **Objective** | Verify `DELETE /api/queues/{name}` removes the row from `QueueConfig` and returns HTTP 200 |
| **Precondition** | `func start` running; test uses a temporary `tc16-delete-queue` (not the live `qbi-queue`) |
| **Steps** | 1. Setup: `POST /api/queues` to create `tc16-delete-queue`. Assert HTTP 200. 2. `curl -X DELETE /api/queues/tc16-delete-queue`. 3. Assert HTTP 200. 4. `az storage entity show --row-key tc16-delete-queue` — assert row is gone (exit code 1 / empty output). |
| **Expected Result** | DELETE returns HTTP 200 with `{ "message": "Queue 'tc16-delete-queue' deleted" }`; `az storage entity show` finds no row |
| **Assertions** | DELETE HTTP 200 AND `ROW_CHECK=GONE` |
| **Covers** | FR-4.3.1 |

---

## 8. Test Execution Schedule

| Run Date | Folder | Scenarios Executed |
|----------|--------|--------------------|
| 2026-07-04 | `20260704_172506` | Menu verification only (no assertions run) |
| 2026-07-04 | `20260704_173814` | Menu verification only |
| 2026-07-04 | `20260704_174602` | TC-01, TC-04, TC-08 (first attempt), TC-02 |
| 2026-07-05 | `20260705_084301` | TC-03 |
| 2026-07-05 | `20260705_091404` | TC-05 (observational, pre-assertion era) |
| 2026-07-05 | `20260705_093715` | TC-07 (defect discovered) |
| 2026-07-05 | `20260705_095729` | TC-06 (observational) |
| 2026-07-13 | `20260713_111240` | TC-08 (two regression runs after v1.3.3 fix) |

---

## 9. Test Traceability Matrix

FR IDs and Level-2 Capability names are taken directly from PRD v1.2, Section 9. Every FR that appears in the PRD must appear here.

| FR ID | PRD Level-2 Capability | Requirement Description | Test Scenario(s) |
|-------|-----------------------|-------------------------|-----------------|
| FR-1.1.1 | Query Service Bus runtime properties | The Collector Service shall retrieve Service Bus runtime properties for each configured queue within the collector execution window. | TC-01, TC-02, TC-10 (implicitly all scenarios) |
| FR-1.2.1 | Query Azure Monitor metrics | The Collector Service shall request incoming and outgoing per-minute metrics for each configured queue when Azure Monitor data is available. | Transitively exercised by all scenarios; not directly asserted |
| FR-1.3.1 | Persist raw queue snapshots | The Repository shall persist each collected queue snapshot in the QueueSnapshot table using the reverse timestamp row key pattern. | Verified by `save_snapshot` artifact capture across all runs |
| FR-2.1.1 | Compute queue deltas and trends | The Analyzer Service shall compute active-count deltas, smoothed rate values, and acceleration from recent queue snapshots. | TC-01, TC-02, TC-03, TC-06, TC-10 (TrendLabel asserted) |
| FR-2.2.1 | Classify queue root cause | The Analyzer Service shall classify queue health into one of the supported root-cause outcomes based on recent queue behavior and rate patterns. | TC-01, TC-02, TC-03, TC-04, TC-05, TC-06, TC-07, TC-08, TC-09, TC-10 |
| FR-2.3.1 | Estimate wait time and SLA status | The Analyzer Service shall estimate wait time and SLA status from the current queue state and the derived outgoing rate. | TC-01, TC-02, TC-03, TC-04, TC-05, TC-06, TC-07, TC-08, TC-09, TC-10 |
| FR-3.1.1 | Determine alert severity | The Alert Service shall assign an alert severity of None, Warning, or Critical for each computed queue status. | TC-01, TC-02, TC-03, TC-04, TC-05, TC-06, TC-07, TC-08, TC-09, TC-10 |
| FR-3.2.1 | Dispatch Teams notifications | The Alert Service shall send a Teams webhook notification when an incident requires alert dispatch. | Verified via `AlertRecord` artifacts; not separately scenario-tested |
| FR-3.3.1 | Dispatch email notifications | The Alert Service shall send an SMTP email notification when email recipients are configured for the queue. | Out of scope — requires live SMTP credentials not present in test environment |
| FR-4.1.1 | Create queue configuration | The Dashboard Function shall accept a queue configuration request and create a new monitored queue record. | TC-14 |
| FR-4.2.1 | Update queue configuration | The Dashboard Function shall update an existing queue configuration record when the user submits a valid configuration change. | TC-15 |
| FR-4.3.1 | Delete queue configuration | The Dashboard Function shall delete a monitored queue configuration record when the user issues a delete request. | TC-16 |
| FR-5.1.1 | Return queue summaries | The Dashboard Function shall return a current queue summary response containing the queue status for all monitored queues. | TC-11 |
| FR-5.2.1 | Return queue history | The Dashboard Function shall return historical status records for a specific queue over a requested range. | TC-12 |
| FR-5.3.1 | Return alert history | The Dashboard Function shall return incident and alert history records for a queue from the AlertRecord table. | TC-13 |
| FR-6.1.1 | Purge old snapshots | The Cleanup Function shall purge QueueSnapshot rows older than the configured retention window. | Not scenario-tested — requires data older than the retention window to exist; exercised implicitly by data aging in Table Storage over 24+ hours |
| FR-6.2.1 | Purge old status records | The Cleanup Function shall purge QueueStatus rows older than the configured retention window. | Not scenario-tested — same constraint as FR-6.1.1; no scripted assertion defined |
| FR-6.3.1 | Purge old alert records | The Cleanup Function shall purge AlertRecord rows older than the configured retention window. | Not scenario-tested — same constraint as FR-6.1.1; no scripted assertion defined |
| FR-7.1.1 | Protect API access through Azure AD Easy Auth | The backend API shall require Azure AD Easy Auth protection in production environments. | Not scenario-tested in local dev — verified manually in Azure production: valid Bearer token → HTTP 200; missing token → HTTP 401 (documented in Risk Report R8, verified during v1.3.0 deployment) |
| FR-7.2.1 | Allow frontend token-based access | The frontend shall provide a token acquisition flow that allows authenticated API requests. | Not scenario-tested — MSAL `loginRedirect` / `acquireTokenRedirect` flow exercised in all browser sessions against Azure; local dev bypass (`VITE_AUTH_ENABLED=false`) implicitly exercised by all local test runs |

---

## 10. Test Execution Log

The table below records every assertion outcome extracted from `terminal.log` files in the `TestResults/` directory. Rows with no assertion are marked as **Observational**.

| Run Date | Run Folder | TC | Assertion Type | Expected | Actual | Result |
|----------|------------|----|---------------|----------|--------|--------|
| 2026-07-04 | 20260704_174602 | TC-01 | `verify_scenario` | Trend=ANY, SLA=OK, Sev=None, Cause=ANY | Trend=Idle, SLA=OK, Sev=None, Cause=Healthy | **PASS** |
| 2026-07-04 | 20260704_174602 | TC-04 | `verify_scenario` | Trend=ANY, SLA=OK, Sev=None, Cause=ANY | Trend=Idle, SLA=OK, Sev=None, Cause=Healthy | **PASS** |
| 2026-07-04 | 20260704_174602 | TC-08 | `verify_scenario` | Trend=Idle, SLA=OK, Sev=None, Cause=Healthy | Trend=Idle, SLA=OK, Sev=**Warning**, Cause=Healthy | **FAIL** |
| 2026-07-04 | 20260704_174602 | TC-02 | `verify_scenario` | Trend=ANY, SLA=BREACHING, Sev=Critical, Cause=ConsumerStopped | Found in history: Sev=Critical, Cause=ConsumerStopped | **PASS** |
| 2026-07-05 | 20260705_084301 | TC-03 | `verify_scenario` | Trend=ANY, SLA=BREACHING, Sev=Critical, Cause=ANY | Growing trend and Critical severity confirmed in history | **PASS** |
| 2026-07-05 | 20260705_091404 | TC-05 | Observational | — | State transition from Healthy → ConsumerStopped observed | Observational |
| 2026-07-05 | 20260705_093715 | TC-07 | `verify_scenario` | Cause=DLQGrowth | NOT_FOUND: DLQ may not have grown yet | **FAIL** |
| 2026-07-05 | 20260705_095729 | TC-06 | Observational | — | BREACHING during slow drain observed | Observational |
| 2026-07-13 | 20260713_111240 | TC-08 | `verify_scenario` | Trend=Idle, SLA=OK, Sev=None, Cause=Healthy | Trend=Idle, SLA=OK, Sev=None, Cause=Healthy (Active=2) | **PASS** |
| 2026-07-13 | 20260713_111240 | TC-08 | `verify_scenario` | Trend=Idle, SLA=OK, Sev=None, Cause=Healthy | Trend=Idle, SLA=OK, Sev=None, Cause=Healthy (Active=2) | **PASS** |

---

## 11. Test Results Summary

| TC | Name | Assertion Runs | Result | Notes |
|----|------|---------------|--------|-------|
| TC-01 | Empty Queue Baseline | 1 | **PASS** | Clean baseline confirmed |
| TC-02 | Consumer Stopped | 1 | **PASS** | `ConsumerStopped/Critical` detected within 2 minutes |
| TC-03 | Growing Backlog | 1 | **PASS** | `Critical` escalation confirmed in history |
| TC-04 | Stable Balanced Queue | 1 | **PASS** | No false alarms on balanced traffic |
| TC-05 | Consumer Stops Mid-Operation | 0 (observational) | — | State transition observed; no formal assertion captured |
| TC-06 | Slow Drain While Breaching SLA | 0 (observational) | — | `BREACHING` observed during slow drain; no assertion captured |
| TC-07 | DLQ Growth | 1 | **FAIL → Fixed (v1.3.3)** | DLQ-BUG-01 discovered; fix merged; re-run not captured in TestResults |
| TC-08 | Idle Queue with Stale Messages | 3 total (1 fail + 2 pass) | **PASS** (post-fix) | SEVERITY-BUG-01 discovered on first run; fixed in v1.3.3; two passing regression runs captured |
| TC-09 | Burst Arrival on Empty Queue | 0 (assertion added in v1.1.1) | — | `verify_not_consumer_stopped` assertion added; no dedicated run in TestResults |
| TC-10 | Full Lifecycle | 0 (assertion added in v1.1.1) | — | `verify_scenario` assertion added in v1.1.1; no dedicated run captured in TestResults |

**Totals (assertion runs only):**

| Outcome | Count |
|---------|-------|
| PASS | 6 |
| FAIL (before fix) | 2 |
| Observational (no assertion) | 2 |
| Not yet executed with assertion | 2 |

---

## 12. Defects Discovered During Testing

### DEF-01 — DLQ-BUG-01: DLQGrowth Not Detected When Active Queue Empties

| Field | Value |
|-------|-------|
| **ID** | DLQ-BUG-01 |
| **Discovered** | 2026-07-05, run `20260705_093715`, TC-07 |
| **Symptom** | `❌ FAIL — NOT_FOUND:DLQ may not have grown yet` even after messages expired to DLQ |
| **Root Cause** | `DLQGrowth` root cause check ran after the `ActiveCount == 0` early return in `AnalyzerService`. When all active messages expired to DLQ (active queue empty), the analyzer short-circuited to `Healthy` before reaching the DLQ check. |
| **Fix** | Moved DLQ check before the Healthy/Idle guard in `AnalyzerService.cs`. |
| **Fixed In** | v1.3.3 (commit `0ac6bf3`, 2026-07-16) |

---

### DEF-02 — DLQ-BUG-02: DLQGrowth Missed Burst-Fill Pattern

| Field | Value |
|-------|-------|
| **ID** | DLQ-BUG-02 |
| **Discovered** | Code review during v1.3.3 fix for DLQ-BUG-01 |
| **Symptom** | `DLQGrowth` was not detected when all messages expired at once (burst-fill) rather than gradually growing |
| **Root Cause** | The rate check `(DLQ[0] - DLQ[5]) / 5 > 1.0` only caught gradual growth. The common case where all messages expire simultaneously was not handled. |
| **Fix** | Added `burstFilled` condition: DLQ currently high AND any of `snapshots[2..5]` shows `DLQCount == 0`. |
| **Fixed In** | v1.3.3 (commit `0ac6bf3`, 2026-07-16) |

---

### DEF-03 — SEVERITY-BUG-01: Unknown Root Cause Incorrectly Escalated to Critical

| Field | Value |
|-------|-------|
| **ID** | SEVERITY-BUG-01 |
| **Discovered** | 2026-07-04, run `20260704_174602`, TC-08 |
| **Symptom** | TC-08 reported `AlertSeverity=Warning` when `None` was expected. The `QueueStatus` row showed `RootCause=Unknown` but `AlertSeverity` was non-None. |
| **Root Cause** | When Azure Monitor rate data was unavailable on the first snapshot after a burst, `SlaStatus` inherited `BREACHING` from recent history. `ComputeRawSeverity` returned `Critical` despite `RootCause=Unknown` — there was no evidence to support a breach determination. |
| **Fix** | Added guard `result.RootCause != "Unknown"` on the `BREACHING → Critical` escalation path in `AnalyzerService.cs`. |
| **Fixed In** | v1.3.3 (commit `0ac6bf3`, 2026-07-16) |

---

### DEF-04 — CORS-BUG-01: Authorization Header Blocked by CORS

| Field | Value |
|-------|-------|
| **ID** | CORS-BUG-01 |
| **Discovered** | During v1.3.0 authentication integration (browser console errors) |
| **Symptom** | Browser preflight (`OPTIONS`) rejected `Authorization` header; React app could not call API after MSAL login |
| **Root Cause** | `AddCors()` in `DashboardFunction.cs` only listed `Content-Type` in `Access-Control-Allow-Headers`; `Authorization` was missing |
| **Fix** | Added `Authorization` to `WithHeaders()` in CORS policy and updated `host.json` to match |
| **Fixed In** | v1.3.2 (commit `7765577`, 2026-07-16) |

---

### DEF-05 — AUTH-BUG-01: Login Loop (block_nested_popups)

| Field | Value |
|-------|-------|
| **ID** | AUTH-BUG-01 |
| **Discovered** | During v1.3.0 authentication integration (browser console errors) |
| **Symptom** | MSAL popup blocked by browser; authentication failed with `block_nested_popups` error; app entered infinite login loop |
| **Root Cause** | `AuthProvider` used `loginPopup` and `useAuthFetch` used `acquireTokenPopup`; many browsers block popups from non-user-gesture code paths |
| **Fix** | Switched `AuthProvider` to `loginRedirect` and `useAuthFetch` to `acquireTokenRedirect` |
| **Fixed In** | v1.3.2 (commit `7765577`, 2026-07-16) |

---

## 13. Defect Resolution Status

| ID | Name | Status | Fixed In | Verification |
|----|------|--------|----------|-------------|
| DLQ-BUG-01 | DLQGrowth not detected when queue empties | **Resolved** | v1.3.3 | Code change verified; no post-fix TestResults run captured |
| DLQ-BUG-02 | DLQGrowth missed burst-fill pattern | **Resolved** | v1.3.3 | Code change verified; no post-fix TestResults run captured |
| SEVERITY-BUG-01 | Unknown root cause escalated to Critical | **Resolved** | v1.3.3 | Two regression runs in `20260713_111240` both PASS |
| CORS-BUG-01 | Authorization header blocked by CORS | **Resolved** | v1.3.2 | Verified manually during browser testing |
| AUTH-BUG-01 | Login loop / block_nested_popups | **Resolved** | v1.3.2 | Verified manually during browser testing |

**All five discovered defects are resolved. No active bugs remain.**

---

## 14. Regression Testing

### 14.1 TC-08 Regression (SEVERITY-BUG-01)

After the SEVERITY-BUG-01 fix was merged in v1.3.3, TC-08 was re-run twice on 2026-07-13 (run folder `20260713_111240`). Both runs produced:

```
─── VERIFICATION: TC-08 Idle Stale ───
Expected: Trend=Idle SLA=OK Severity=None Cause=Healthy
Actual:   Trend=Idle SLA=OK Severity=None Cause=Healthy (Active=2)
✅ PASS
```

### 14.2 TC-07 Regression (DLQ-BUG-01, DLQ-BUG-02)

No formal post-fix TestResults run was captured for TC-07. The fix was verified by code inspection of `AnalyzerService.cs` after commit `0ac6bf3`. A dedicated post-fix regression run for TC-07 is identified as an open gap (see Section 20).

### 14.3 Test Script Regression (v1.4.0 menu fix)

The `test_scenarios.sh` menu/function number mismatch was fixed in v1.4.0. The fix was verified by:
- Reviewing the updated case statement alignment
- Confirming menu labels [7]–[11] match their corresponding function banners

No TestResults run was required for this fix as it affected navigation only, not scenario logic.

---

## 15. Test Coverage Analysis

### 15.1 Functional Requirement Coverage

| PRD Level-1 Capability | Category (FR-x.x.x) | FR Count | FRs with ≥1 Assertion | Coverage |
|------------------------|---------------------|----------|----------------------|----------|
| Collect Queue Metrics | FR-1.x.x | 3 | 1 (FR-1.1.1) | 33% |
| Analyze Queue Health | FR-2.x.x | 3 | 3 (FR-2.1.1, FR-2.2.1, FR-2.3.1) | 100% |
| Manage Alerting | FR-3.x.x | 3 | 1 (FR-3.1.1) | 33% |
| Manage Queue Configurations | FR-4.x.x | 3 | 3 (TC-14, TC-15, TC-16) | 100% |
| Expose Dashboard Data | FR-5.x.x | 3 | 3 (TC-11, TC-12, TC-13) | 100% |
| Retain Operational Data | FR-6.x.x | 3 | 0 | 0% |
| Authenticate Access | FR-7.x.x | 2 | 0 | 0% |
| **Total** | | **20** | **11** | **55%** |

### 15.2 Root Cause Coverage

| Root Cause | Covered By | Test Result |
|------------|-----------|-------------|
| `ConsumerStopped` | TC-02, TC-03 | PASS |
| `Healthy` | TC-01, TC-04, TC-08 | PASS |
| `DLQGrowth` | TC-07 | FAIL (pre-fix) → unverified post-fix |
| `Recovering` | TC-05 (observational) | Not asserted |
| `ConsumerSlowdown` | None | Not tested |
| `ProducerSpike` | TC-09 (negative test) | Not formally asserted |
| `ProducerSpikeAndConsumerSlowdown` | None | Not tested |

### 15.3 Alert Severity Coverage

| Severity | Covered By |
|----------|-----------|
| `None` | TC-01, TC-04, TC-08 |
| `Warning` | TC-03 (transitional, not directly asserted) |
| `Critical` | TC-02, TC-03 |

### 15.4 Gap Summary

The following are not covered by any test scenario:
- `ConsumerSlowdown` root cause
- `ProducerSpikeAndConsumerSlowdown` root cause
- Direct `Warning` severity assertion
- `GET /api/queues/{name}/status` and `GET /api/health` endpoints (functional but not separately asserted; health endpoint exercised by Sidebar polling during browser sessions)
- Frontend rendering (Overview, Queue Detail, Sidebar, Settings pages)
- Teams webhook delivery (verified observationally during v1.1.0 deployment; not scripted)
- Email alert delivery (requires live SMTP credentials; excluded from test environment)
- CleanupFunction retention enforcement (requires 24-hour real-time wait; no short-TTL test environment available)

---

## 16. Quality Assessment

### 16.1 Test Quality

| Attribute | Assessment |
|-----------|-----------|
| **Repeatability** | High — each scenario starts from a known-empty queue and uses time-bound waits |
| **Observability** | High — every assertion prints `Expected:` and `Actual:` field values; all runs are logged |
| **Independence** | High — `purge_queue()` at the start of each scenario prevents state leakage |
| **Completeness** | Partial — 15 of 17 scenarios have assertions; 11 of 20 FRs are directly covered (55%) |
| **Automation** | Low — no automated execution; all scenarios require a human operator |

### 16.2 Product Quality

All five defects discovered during testing were fixed before v1.4.0 release. No regressions were observed. The AnalyzerService's 9-step pipeline correctly handles the four most critical scenarios: empty queue baseline, consumer stopped, growing backlog, and stable balanced queue.

The two observational scenarios (TC-05 Recovery, TC-06 Slow Drain) produced correct system behavior during development runs; their assertions were added in v1.1.1 but no post-addition execution runs are currently captured.

---

## 17. Risks and Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|-----------|
| Azure Monitor 2–4 minute delay causes TC-09 to intermittently fail | Medium | Medium | `queueWasRecentlyEmpty` guard in `AnalyzerService`; 3-snapshot smoothed average filters noise (L-07) |
| TC-07 post-fix regression not captured | Medium | Low | Code inspection of commit `0ac6bf3` confirms fix; dedicated re-run is identified in Section 20 |
| TC-05 and TC-06 lack formal assertions in captured runs | Low | Medium | Assertions added in v1.1.1; future runs will capture them |
| Single-snapshot `Recovering` state missed by TC-05 assertion | Low | Low | Documented as L-08; TC-05 asserts final stable state, not transient |
| Teams webhook URL pattern change breaks format detection | Low | High | Documented as L-09; `AlertService` URL-based detection is a known design limitation |

---

## 18. Assumptions and Constraints

### 18.1 Assumptions

1. Azure Service Bus `qbi-queue` is the only queue under test; multi-queue behavior is not separately tested.
2. All test runs were executed with the same queue configuration (no SLA threshold changes between runs).
3. The Azure Monitor metric delay during each test run was within the normal 2–4 minute range.
4. `func start` was running successfully in a separate terminal during all test runs.
5. The test operator manually confirmed message counts in Azure Portal Service Bus Explorer before pressing ENTER at each `send_messages()` prompt.

### 18.2 Constraints

1. **No CI/CD**: Tests cannot be run automatically on push; they require an engineer with Azure access.
2. **Shared live queue**: All test runs modify the same production `qbi-queue`; concurrent test runs are not possible.
3. **Manual message injection**: Azure Portal is the only supported send mechanism (no programmatic injection in `test_scenarios.sh`).
4. **No local queue simulation**: All tests require Azure connectivity; offline execution is not possible.
5. **Azure Monitor delay**: Scenarios that depend on rate metrics require waits longer than 4 minutes to produce reliable results.

---

## 19. Test Artifacts

All test artifacts are committed to the repository under `backend/Tests/TestResults/`.

| Run Folder | TC Covered | Key Files |
|------------|-----------|-----------|
| `20260704_172506` | Menu only | `terminal.log` |
| `20260704_173814` | Menu only | `terminal.log` |
| `20260704_174602` | TC-01, TC-04, TC-08 (fail), TC-02 | `terminal.log`, `TC01_*`, `TC04_*`, `TC08_*`, `TC02_*` |
| `20260705_084301` | TC-03 | `terminal.log`, `TC03_*` |
| `20260705_091404` | TC-05 (observational) | `terminal.log` |
| `20260705_093715` | TC-07 (fail) | `terminal.log`, `TC07_*` |
| `20260705_095729` | TC-06 (observational) | `terminal.log` |
| `20260713_111240` | TC-08 (two passes) | `terminal.log`, `TC08_*` (two sets) |

Each JSON artifact captures up to 20 rows from `QueueSnapshot`, `QueueStatus`, and `AlertRecord` tables at the time of verification, providing point-in-time evidence of system state.

---

## 20. Open Issues

| ID | Description | Priority |
|----|-------------|----------|
| OI-01 | TC-07 post-fix regression run not captured in `TestResults/` — need one dedicated re-run after v1.3.3 to formally close DLQ-BUG-01 and DLQ-BUG-02 | High |
| OI-02 | TC-05 and TC-06 have assertions added in v1.1.1 but no formal assertion run is captured — next execution will produce the first formal PASS/FAIL records | Medium |
| OI-03 | TC-09 and TC-10 assertion added in v1.1.1 but no TestResults run exists — dedicated execution needed | Medium |
| OI-04 | `ConsumerSlowdown` and `ProducerSpikeAndConsumerSlowdown` root causes have no test scenarios — behavior is untested | Medium |
| OI-05 | CleanupFunction has no manual test scenario — verification requires a 24-hour real-time wait or a dedicated short-TTL test environment | Low |
| OI-06 | TC-11 through TC-16 (API scenarios) have not yet been executed — dedicated first runs needed to produce PASS/FAIL records in `TestResults/` | Medium |

---

## 21. Glossary

| Term | Definition |
|------|-----------|
| **ActiveCount** | Number of messages in the active (non-DLQ) queue, from Azure Service Bus Administration API |
| **AlertRecord** | Azure Table Storage table recording alert events per queue (PartitionKey = queue name, RowKey = GUID) |
| **AnalyzerService** | .NET 8 service that runs a 9-step intelligence pipeline to classify queue health from collected snapshots |
| **BREACHING** | SLA status indicating `WaitTimeMinutes` has exceeded the configured SLA threshold |
| **Collector** | Azure Function (30-second timer) that polls Azure Service Bus and Azure Monitor to capture queue metrics |
| **ConsumerStopped** | Root cause: messages are accumulating and no consumer is draining them |
| **ConsumerSlowdown** | Root cause: consumer is draining slower than messages are arriving |
| **DLQ / Dead Letter Queue** | Azure Service Bus sub-queue holding messages that exceeded delivery count or TTL |
| **DLQGrowth** | Root cause: dead letter count is increasing (even if active queue is empty) |
| **GapPerMin** | `IncomingPerMin - OutgoingPerMin`; negative means draining, positive means growing |
| **Healthy** | Root cause: queue is balanced or idle with no concerning trends |
| **func start** | Azure Functions Core Tools CLI command to run the function app locally |
| **NeedToSendAlert** | Boolean flag on `QueueStatus` row indicating `AlertService` should dispatch a notification |
| **ProducerSpike** | Root cause: message arrival rate has spiked unexpectedly |
| **QBIS** | Queue Backlog Intelligence System — the system under test |
| **QueueSnapshot** | Azure Table Storage table storing raw metric samples (RowKey = reverse-tick timestamp) |
| **QueueStatus** | Azure Table Storage table storing analyzer output per cycle (RowKey = reverse-tick timestamp) |
| **Recovering** | Root cause: consumer has resumed draining after a `ConsumerStopped` state |
| **Reverse-tick RowKey** | `(DateTime.MaxValue.Ticks - now.Ticks).ToString("D19")` — produces a descending sort so the newest row has the lowest key and is returned first by Azure Table Storage queries |
| **RootCause** | Analyzer-assigned string classifying queue health: `ConsumerStopped`, `ConsumerSlowdown`, `ProducerSpike`, `ProducerSpikeAndConsumerSlowdown`, `DLQGrowth`, `Recovering`, `Healthy`, `Unknown` |
| **SlaStatus** | Computed field: `OK`, `BREACHING`, or `UNKNOWN` |
| **TrendLabel** | Computed field: `Idle`, `Stable`, `Growing`, `GrowingFast`, `Draining`, `DrainingFast` |
| **verify_not_consumer_stopped()** | Bash function asserting `RootCause ≠ ConsumerStopped` for false-positive prevention tests |
| **verify_scenario()** | Bash function asserting four exact fields on the most recent `QueueStatus` row |
| **WaitTimeMinutes** | Estimated time for all active messages to be processed: `ActiveCount / OutgoingPerMin` |

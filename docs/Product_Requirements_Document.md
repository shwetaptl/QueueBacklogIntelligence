# Queue Backlog Intelligence System (QBIS)

## Cover Page

- Project Name: Queue Backlog Intelligence System (QBIS)
- Student(s): Shweta Patel
- Course: CISC 593/594
- Semester: To Be Completed
- Repository URL: https://github.com/shwetaptl/QueueBacklogIntelligence.git
- Current Branch: main
- Current Commit SHA: 74e9f743fbe37fd6f0eafde1c926ac6bb5d317fe
- Current Release Version: v1.4.1
- Document Version: v1.3
- Last Updated: 2026-08-06

---

## Revision History

| Version | Date | Git Commit | Description | Author |
|----------|------|------------|-------------|--------|
| v1.0 | 2026-07-21 | 9e37977e5a0a8517884b81e37c3ee480b76dfde1 | Initial PRD creation aligned to repository evidence for the current QBIS implementation. | Shweta Patel |
| v1.1 | 2026-07-21 | 74e9f743fbe37fd6f0eafde1c926ac6bb5d317fe | Updated cover page (student name, commit SHA, release version v1.4.1, document version); resolved stakeholder and author To Be Completed items traceable from the repository. | Shweta Patel |
| v1.2 | 2026-07-21 | 082d3dc | Added Section 8 Preventative Requirements (Q2 explicit layer); renumbered subsequent sections 8–17 to 9–18. | Shweta Patel |
| v1.3 | 2026-08-06 | — | Added Section 1.5 Comparative Positioning — qualitative benchmark of QBIS 9-step pipeline against Azure Monitor built-in metric alerts and a naive threshold-only custom checker across eight operational scenarios. | Shweta Patel |

---

## Table of Contents

1. [Product Vision](#1-product-vision) _(includes §1.5 Comparative Positioning)_
2. [Product Scope](#2-product-scope)
3. [Software Capabilities](#3-software-capabilities)
4. [Undesirable Events](#4-undesirable-events)
5. [Risk Analysis](#5-risk-analysis)
6. [Risk Prioritization](#6-risk-prioritization)
7. [Risk Mitigation](#7-risk-mitigation)
8. [Preventative Requirements](#8-preventative-requirements)
9. [Functional Requirements](#9-functional-requirements)
10. [Quality Requirements](#10-quality-requirements)
11. [Performance Requirements](#11-performance-requirements)
12. [Assumptions](#12-assumptions)
13. [Constraints](#13-constraints)
14. [External Interfaces](#14-external-interfaces)
15. [Requirements Traceability Matrix](#15-requirements-traceability-matrix)
16. [Future Versions](#16-future-versions)
17. [Open Issues](#17-open-issues)
18. [Glossary](#18-glossary)

---

# 1. Product Vision

## Problem Statement

When a Service Bus queue backs up, on-call engineers need an immediate answer to whether a queue is unhealthy, why it is unhealthy, and how long until an SLA breach occurs. The repository implements QBIS as a serverless monitoring and alerting system that continuously collects queue metrics, classifies backlog conditions, and surfaces actionable status through a live dashboard.

## Intended Users

- On-call engineers monitoring Azure Service Bus queue health
- Platform or operations engineers responsible for queue reliability
- Developers or administrators configuring monitored queues and alert destinations

## Stakeholders

- Repository owner and project developer: Shweta Patel
- Azure infrastructure and operations stakeholders: To Be Completed
- End users of the monitoring dashboard: To Be Completed

## Product Goals

- Detect queue backlog and SLA risk from live Service Bus metrics
- Classify queue health using a computed intelligence pipeline
- Reduce alert noise through deduplication and sticky severity logic
- Provide a dashboard with queue overview and per-queue detail views
- Persist operational status and alert history in Azure Table Storage

## Major Features

- Multi-queue dashboard overview with KPI tiles and queue health grid
- Per-queue detail history and trend views
- Service Bus metric collection and queue status analysis
- Root cause classification for conditions such as ConsumerStopped, ConsumerSlowdown, ProducerSpike, DLQGrowth, and Recovering
- Teams and SMTP alert dispatch with severity escalation and reminder behavior
- Queue configuration management through the dashboard Settings view
- Daily cleanup of older records to control storage growth

## Planned Software Versions

- Current release in repository: v1.4.1
- Prior documented release history exists in [CHANGELOG.md](../CHANGELOG.md)
- Future versions are described in Section 17 and are subject to change

## Comparative Positioning

This section establishes why QBIS uses a 9-step deterministic pipeline rather than one of two simpler alternatives an operations team might reach for first. The comparison is qualitative and grounded in the specific failure modes that motivated each design decision.

### Baseline A — Azure Monitor Built-in Metric Alerts

Azure Monitor allows a rule such as "alert when `ActiveMessages > 500` for 5 minutes." This is the lowest-effort monitoring option, requiring no custom code.

**What it provides:**
- A single threshold alert when a count crosses a configured value
- Configurable severity (Sev 0–4) and action groups for notification routing

**What it cannot do:**
- Cannot distinguish *why* the count crossed the threshold (consumer stopped vs. producer spike vs. DLQ overflow)
- Fires 2–4 minutes after the event due to Azure Monitor ingestion delay; QBIS uses the Service Bus Administration API directly, which reflects the current active count in near real-time
- Cannot detect DLQ growth when `ActiveMessageCount` is zero — a queue where all messages have expired to DLQ appears healthy to a count-threshold rule
- Cannot classify a recovering queue — a queue draining back to zero still triggers "count > threshold" alerts until it physically falls below the line
- Cannot suppress false positives from burst arrivals on previously empty queues; a sudden producer spike looks identical to a consumer outage
- No SLA wait-time estimation — the alert fires or does not; it does not tell the on-call engineer how many minutes until a breach

### Baseline B — Naive Threshold-Only Custom Checker

A slightly more sophisticated custom solution might check `ActiveCount > threshold` every minute and send a Teams message. This eliminates the Azure Monitor ingestion delay but still applies a single numeric guard.

**What it provides over Baseline A:**
- Near-real-time check via the Service Bus Admin API (same source as QBIS)
- Customisable threshold and cadence without Azure Monitor dependency

**What it still cannot do:**
- Still fires the same alert for ConsumerStopped and ProducerSpike — both cause `ActiveCount` to grow; the remediation actions are entirely different (restart the consumer vs. scale producer capacity), yet the alert text is identical
- DLQ growth on an idle queue remains invisible (ActiveCount = 0 → checker sees "healthy")
- Recovering queues continue to fire alerts because count is still above threshold, even as the consumer is actively draining
- A single OK reading (count falls below threshold for one cycle) clears the alert, even if the queue is oscillating; QBIS requires two consecutive OK readings before de-escalating (sticky severity rule, PR-3.1-01)
- Alert flapping: without sticky severity, every oscillation above/below the threshold generates a new alert and a new clearance, producing noise that on-call engineers learn to ignore
- No root cause text in the alert — the message says "queue is high" but not why

### QBIS 9-Step Pipeline — What the Custom Logic Adds

| Scenario | Azure Monitor | Threshold Checker | QBIS |
|---|---|---|---|
| ConsumerStopped vs ProducerSpike | Same alert for both | Same alert for both | Differentiates via outgoing-rate check: zero outgoing → ConsumerStopped; high incoming, normal outgoing → ProducerSpike |
| Burst on empty queue | Fires Critical (2–4 min late) | Fires Critical immediately | Suppresses ConsumerStopped for 2 min via `queueWasRecentlyEmpty` guard (PR-2.2-01) |
| DLQ growth, ActiveCount = 0 | No alert — count threshold not breached | No alert — count threshold not breached | Fires DLQGrowth alert regardless of ActiveCount |
| Recovering queue (actively draining) | Continues alerting until count falls below threshold | Continues alerting until count falls below threshold | Classifies as Recovering, downgrades severity, suppresses re-alert |
| Alert flapping (oscillating count) | Fires and clears on every cycle | Fires and clears on every cycle | Requires 2 consecutive OK readings before de-escalating (sticky severity) |
| SLA breach risk | No estimation | No estimation | Computes `WaitTimeMinutes` from outgoing rate; shows time to breach on dashboard |
| Root cause for on-call engineer | None | None | Root cause label (ConsumerStopped, ProducerSpike, DLQGrowth, Recovering, etc.) in alert and dashboard |
| Azure Monitor rate data unavailable | Alert still fires on count | n/a (does not use Monitor) | Falls back to count-only logic; never fires Critical for Unknown root cause (PR-2.2-02) |
| ConsumerSlowdown (rate declining, not stopped) | Fires only if count exceeds threshold | Fires only if count exceeds threshold | Detects early via outgoing-rate deceleration before threshold breach |

The 9-step pipeline addresses each gap in the table above. The design trade-off is implementation complexity: the pipeline requires maintaining a rolling snapshot history, computing smoothed rate averages, and encoding priority-ordered root-cause logic. That complexity is the cost; the benefit is that on-call engineers receive a root cause label and an estimated breach window instead of a generic "count is high" notification.

---

# 2. Product Scope

## Included Functionality

- Monitoring of Azure Service Bus queues through Azure Functions timer triggers
- Collection of queue activity and dead-letter metrics
- Analysis of queue behavior into computed status and trend labels
- Alert generation and dispatch to Microsoft Teams and SMTP email
- Dashboard support for multi-queue health, queue history, and settings
- Storage of queue configurations, snapshots, status results, and alert records in Azure Table Storage
- Retention cleanup for historical data

## Excluded Functionality

- Bulk queue message replay or message remediation
- Non-Service Bus queue monitoring
- User management beyond current dashboard access model
- Full enterprise incident management workflows
- Automated remediation or self-healing actions

## Future Enhancements

- Expanded alerting and integration options beyond current Teams and SMTP support
- Additional dashboards and analytics views
- More configurable thresholds and tuning controls
- Support for more queue namespaces or broader Azure monitoring scenarios
- Additional automation around incident response

---

# 3. Software Capabilities

## 3.1 Level-1 Capabilities

The repository supports the following seven major capabilities:

1. Collect Queue Metrics
2. Analyze Queue Health
3. Manage Alerting
4. Manage Queue Configurations
5. Expose Dashboard Data
6. Retain Operational Data
7. Authenticate Access

## 3.2 Level-2 Capabilities

### 1. Collect Queue Metrics

1.1 Query Service Bus runtime properties

1.2 Query Azure Monitor metrics

1.3 Persist raw queue snapshots

### 2. Analyze Queue Health

2.1 Compute queue deltas and trends

2.2 Classify queue root cause

2.3 Estimate wait time and SLA status

### 3. Manage Alerting

3.1 Determine alert severity

3.2 Dispatch Teams notifications

3.3 Dispatch email notifications

### 4. Manage Queue Configurations

4.1 Create queue configuration

4.2 Update queue configuration

4.3 Delete queue configuration

### 5. Expose Dashboard Data

5.1 Return queue summaries

5.2 Return queue history

5.3 Return alert history

### 6. Retain Operational Data

6.1 Purge old snapshots

6.2 Purge old status records

6.3 Purge old alert records

### 7. Authenticate Access

7.1 Protect API access through Azure AD Easy Auth

7.2 Allow frontend token-based access for authenticated requests

---

# 4. Undesirable Events

| UE ID | Level-2 Capability | Undesirable Event |
|------|--------------------|-------------------|
| UE-1.1-01 | Query Service Bus runtime properties | Collector fails to retrieveActiveCount or DLQCount from the queue runtime API |
| UE-1.2-01 | Query Azure Monitor metrics | Incoming and outgoing rate data are unavailable or delayed, causing unreliable rate-based decisions |
| UE-1.3-01 | Persist raw queue snapshots | Snapshot writes are skipped or incomplete, preventing the analyzer from building a correct time series |
| UE-2.1-01 | Compute queue deltas and trends | Delayed or missing snapshots create incorrect delta and acceleration values |
| UE-2.2-01 | Classify queue root cause | Queue is misclassified with an incorrect root cause, such as ConsumerStopped instead of a transient burst |
| UE-2.3-01 | Estimate wait time and SLA status | Wait time cannot be calculated when outgoing rate is zero or unreliable, leading to a false breach signal |
| UE-3.1-01 | Determine alert severity | Severity stays at None or de-escalates too early, causing missed or delayed incident escalation |
| UE-3.2-01 | Dispatch Teams notifications | Teams webhook call fails or the payload format is invalid |
| UE-3.3-01 | Dispatch email notifications | SMTP configuration is missing or email delivery fails |
| UE-4.1-01 | Create queue configuration | A queue configuration is created with incomplete or invalid settings |
| UE-4.2-01 | Update queue configuration | Existing queue configuration is overwritten incorrectly during an update |
| UE-4.3-01 | Delete queue configuration | Queue configuration deletion removes data without the intended user confirmation or recovery path |
| UE-5.1-01 | Return queue summaries | Dashboard summary endpoint returns stale or partially missing queue data |
| UE-5.2-01 | Return queue history | History endpoint does not return the requested range or returns misleading ordering |
| UE-5.3-01 | Return alert history | Incident history is incomplete or fails to reflect the latest alert lifecycle |
| UE-6.1-01 | Purge old snapshots | Old snapshot cleanup fails and storage growth remains unbounded |
| UE-6.2-01 | Purge old status records | Old computed status records are not purged, increasing retention cost and query burden |
| UE-6.3-01 | Purge old alert records | Alert records remain beyond retention policy and become difficult to manage |
| UE-7.1-01 | Protect API access through Azure AD Easy Auth | API authentication fails in production or denies legitimate requests |
| UE-7.2-01 | Allow frontend token-based access | Frontend cannot obtain or attach the required authorization token for API access |

> Note: Some entries in this section are derived from README, architecture, and changelog evidence. Where the repository does not document a specific operational policy, the corresponding item remains evidenced only by the implemented code paths.

---

# 5. Risk Analysis

| UE ID | Risk Statement | Likelihood | Impact | Risk Score |
|------|----------------|------------|--------|------------|
| UE-1.1-01 | If Service Bus runtime properties cannot be retrieved, the collector cannot establish baseline backlog and dead-letter count, which may cause incorrect health decisions. | 3 | 4 | 12 |
| UE-1.2-01 | If Azure Monitor data is delayed or unavailable, the analyzer may misclassify rising or draining conditions because rate-based decisions are secondary to the Service Bus Admin API. | 4 | 4 | 16 |
| UE-1.3-01 | If queue snapshots are not persisted, the time-series intelligence pipeline loses the evidence needed to assess trends and incidents. | 3 | 5 | 15 |
| UE-2.1-01 | If delta and trend calculations use incorrect or missing snapshots, the queue state may be misrepresented to the dashboard and alerts. | 3 | 4 | 12 |
| UE-2.2-01 | If root cause classification is wrong, the operations team may pursue the wrong investigation path. | 3 | 4 | 12 |
| UE-2.3-01 | If wait time or SLA status is estimated incorrectly, a queue may be treated as healthy when it is actually breaching. | 3 | 5 | 15 |
| UE-3.1-01 | If alert severity determination is unstable, incidents may be under-reported or repeatedly flapped. | 3 | 4 | 12 |
| UE-3.2-01 | If Teams notifications fail, incident response may be delayed despite the underlying queue issue being valid. | 3 | 4 | 12 |
| UE-3.3-01 | If SMTP email alerting fails, users depending on email delivery will miss the alert. | 3 | 3 | 9 |
| UE-4.1-01 | If queue configuration creation is incomplete, the system may monitor the wrong queue or fail to apply intended thresholds. | 2 | 4 | 8 |
| UE-4.2-01 | If queue configuration updates overwrite settings incorrectly, the queue may breach its SLA without the expected alert behavior. | 2 | 4 | 8 |
| UE-4.3-01 | If a queue configuration is deleted unintentionally, monitoring and alerting may cease for that queue. | 2 | 4 | 8 |
| UE-5.1-01 | If queue summaries are stale or incomplete, the dashboard may mislead operators about the current multi-queue status. | 3 | 3 | 9 |
| UE-5.2-01 | If queue history responses are inconsistent, incident review and time-series analysis may be unreliable. | 2 | 3 | 6 |
| UE-5.3-01 | If alert history is incomplete, incident closure and escalation analysis become difficult. | 2 | 3 | 6 |
| UE-6.1-01 | If snapshot cleanup does not run, storage growth may become unbounded over time. | 3 | 3 | 9 |
| UE-6.2-01 | If status record cleanup does not run, long-lived history may increase query and cost overhead. | 3 | 3 | 9 |
| UE-6.3-01 | If alert record cleanup does not run, retention policy compliance and operational performance may degrade. | 3 | 3 | 9 |
| UE-7.1-01 | If API protection through Azure AD Easy Auth fails, request handling may be blocked or exposed incorrectly in production. | 2 | 5 | 10 |
| UE-7.2-01 | If frontend token acquisition fails, the user experience may degrade because the dashboard cannot access protected API data. | 2 | 3 | 6 |

---

# 6. Risk Prioritization

Sorted by descending risk score.

| Priority | UE ID | Risk Score |
|----------|------|------------|
| 1 | UE-1.2-01 | 16 |
| 2 | UE-1.3-01 | 15 |
| 3 | UE-2.3-01 | 15 |
| 4 | UE-1.1-01 | 12 |
| 5 | UE-2.1-01 | 12 |
| 6 | UE-2.2-01 | 12 |
| 7 | UE-3.1-01 | 12 |
| 8 | UE-3.2-01 | 12 |
| 9 | UE-7.1-01 | 10 |
| 10 | UE-6.1-01 | 9 |
| 11 | UE-6.2-01 | 9 |
| 12 | UE-6.3-01 | 9 |
| 13 | UE-3.3-01 | 9 |
| 14 | UE-5.1-01 | 9 |
| 15 | UE-4.1-01 | 8 |
| 16 | UE-4.2-01 | 8 |
| 17 | UE-4.3-01 | 8 |
| 18 | UE-7.2-01 | 6 |
| 19 | UE-5.2-01 | 6 |
| 20 | UE-5.3-01 | 6 |

---

# 7. Risk Mitigation

| UE ID | Risk Mitigation | Classification |
|------|-----------------|----------------|
| UE-1.2-01 | Use Service Bus Admin API as the primary source of active backlog truth and treat Azure Monitor as secondary data with consistency checks and fallbacks. | Pure Software |
| UE-1.3-01 | Persist every raw snapshot as a table record and validate the collector run before the analyzer proceeds. | Pure Software |
| UE-2.3-01 | Use conservative wait-time behavior when outgoing rate is zero or unreliable, and avoid fabricating a numerical wait value when the system has insufficient evidence. | Pure Software |
| UE-1.1-01 | Add runtime validation and error handling around Service Bus Administration API calls to prevent silent failure paths. | Pure Software |
| UE-2.1-01 | Require a minimum snapshot history and guard against gaps greater than the configured tolerance before interpreting deltas. | Pure Software |
| UE-2.2-01 | Use priority-ordered root-cause logic and retain state transitions across recent history to avoid single-sample misclassification. | Pure Software |
| UE-3.1-01 | Use sticky severity and consecutive OK checks before de-escalation to reduce flapping and false recovery behavior. | Pure Software |
| UE-3.2-01 | Send Teams notifications with payload format detection and catch webhook failures without aborting the rest of the alerting workflow. | Pure Software |
| UE-3.3-01 | Validate SMTP configuration at startup and log delivery failures clearly while preserving the alert lifecycle record. | Pure Software |
| UE-4.1-01 | Validate required queue configuration fields before creating a new queue record. | Pure Software |
| UE-4.2-01 | Use explicit update paths and persistence checks to avoid corrupting existing configuration data. | Pure Software |
| UE-4.3-01 | Require a deliberate delete action and preserve traceability through stored entity data. | Pure Software |
| UE-5.1-01 | Return only precomputed, latest state data and keep the health endpoint focused on liveness rather than replacement of data integrity checks. | Pure Software |
| UE-5.2-01 | Normalize history ordering and date range filtering around the reverse timestamp pattern used by Azure Table Storage. | Pure Software |
| UE-5.3-01 | Store incident records as explicit alert lifecycle artifacts rather than deriving them ad hoc. | Pure Software |
| UE-6.1-01 | Run the retention cleanup function with per-queue and total deletion logging. | Pure Software |
| UE-6.2-01 | Use the same cleanup pattern for computed status rows to reduce accumulation over time. | Pure Software |
| UE-6.3-01 | Apply a retention window to alert records and perform batch deletes to stay within Table Storage limits. | Pure Software |
| UE-7.1-01 | Keep authentication at the Azure Functions infrastructure layer and validate production tokens before request handling. | Hybrid (Software + Hardware) |
| UE-7.2-01 | Use MSAL React token acquisition and support a local development bypass configuration for non-production environments. | Pure Software |

---

# 8. Preventative Requirements

Preventative requirements state what the system must **never** do. Each requirement is a hard invariant that, if violated, directly causes the corresponding Undesirable Event. These are the Q2 layer — distinct from Q1 (desired behavior, Section 9) and Q3 (responsive behavior, Section 7).

| PR ID | UE ID | Preventative Requirement |
|-------|-------|--------------------------|
| PR-2.2-01 | UE-2.2-01 | The Analyzer Service must never classify a burst arrival on a previously empty queue as `ConsumerStopped`; the `queueWasRecentlyEmpty` guard must suppress this classification for at least 2 minutes after a burst. |
| PR-2.2-02 | UE-2.2-01 | The Analyzer Service must never escalate `AlertSeverity` to `Critical` when `RootCause = Unknown`; insufficient evidence must not be treated as a confirmed breach. |
| PR-3.1-01 | UE-3.1-01 | The Alert Service must never de-escalate alert severity from `Critical` or `Warning` after fewer than 2 consecutive OK-severity readings (sticky severity rule). |
| PR-1.2-01 | UE-1.2-01 | The Analyzer Service must never use a single raw Azure Monitor rate snapshot as the sole basis for root cause classification; smoothed values from at least 3 snapshots must be used. |
| PR-2.3-01 | UE-2.3-01 | The Analyzer Service must never report a finite positive `WaitTimeMinutes` value when the outgoing rate is zero or statistically unreliable; the value must be expressed as infinite or indeterminate in that state. |
| PR-3.2-01 | UE-3.2-01 | The Alert Service must never send a legacy MessageCard Teams payload to a non-`webhook.office.com` URL, and must never send an Adaptive Card payload to a `webhook.office.com` URL. |
| PR-6.1-01 | UE-6.1-01 | The Cleanup Function must never delete records that fall within the configured retention window; only rows with a timestamp older than the retention cutoff are eligible for removal. |
| PR-7.1-01 | UE-7.1-01 | The backend API must never expose `ServiceBusConnectionString`, `StorageConnectionString`, or any other credential value in an API response body, HTTP header, or application log entry. |
| PR-4.3-01 | UE-4.3-01 | The Dashboard Function must never delete a queue configuration record through an automated or background process; deletion must occur only in response to an explicit, user-initiated delete request. |

---

# 9. Functional Requirements

| Requirement ID | Level-2 Capability | Functional Requirement |
|----------------|--------------------|------------------------|
| FR-1.1.1 | Query Service Bus runtime properties | The Collector Service shall retrieve Service Bus runtime properties for each configured queue within the collector execution window. |
| FR-1.2.1 | Query Azure Monitor metrics | The Collector Service shall request incoming and outgoing per-minute metrics for each configured queue when Azure Monitor data is available. |
| FR-1.3.1 | Persist raw queue snapshots | The Repository shall persist each collected queue snapshot in the QueueSnapshot table using the current queue partition and reverse timestamp row key pattern. |
| FR-2.1.1 | Compute queue deltas and trends | The Analyzer Service shall compute active-count deltas, smoothed rate values, and acceleration from recent queue snapshots within the analyzer pipeline. |
| FR-2.2.1 | Classify queue root cause | The Analyzer Service shall classify queue health into one of the supported root-cause outcomes based on recent queue behavior and rate patterns. |
| FR-2.3.1 | Estimate wait time and SLA status | The Analyzer Service shall estimate wait time and SLA status from the current queue state and the derived outgoing rate. |
| FR-3.1.1 | Determine alert severity | The Alert Service shall assign an alert severity of None, Warning, or Critical for each computed queue status. |
| FR-3.2.1 | Dispatch Teams notifications | The Alert Service shall send a Teams webhook notification when an incident requires alert dispatch. |
| FR-3.3.1 | Dispatch email notifications | The Alert Service shall send an SMTP email notification when email recipients are configured for the queue. |
| FR-4.1.1 | Create queue configuration | The Dashboard Function shall accept a queue configuration request and create a new monitored queue record. |
| FR-4.2.1 | Update queue configuration | The Dashboard Function shall update an existing queue configuration record when the user submits a valid configuration change. |
| FR-4.3.1 | Delete queue configuration | The Dashboard Function shall delete a monitored queue configuration record when the user issues a delete request. |
| FR-5.1.1 | Return queue summaries | The Dashboard Function shall return a current queue summary response containing the queue status for all monitored queues. |
| FR-5.2.1 | Return queue history | The Dashboard Function shall return historical status records for a specific queue over a requested range. |
| FR-5.3.1 | Return alert history | The Dashboard Function shall return incident and alert history records for a queue from the AlertRecord table. |
| FR-6.1.1 | Purge old snapshots | The Cleanup Function shall purge QueueSnapshot rows older than the configured retention window. |
| FR-6.2.1 | Purge old status records | The Cleanup Function shall purge QueueStatus rows older than the configured retention window. |
| FR-6.3.1 | Purge old alert records | The Cleanup Function shall purge AlertRecord rows older than the configured retention window. |
| FR-7.1.1 | Protect API access through Azure AD Easy Auth | The backend API shall require Azure AD Easy Auth protection in production environments to secure access to the Functions host. |
| FR-7.2.1 | Allow frontend token-based access | The frontend shall provide a token acquisition flow that allows authenticated API requests through the dashboard. |

---

# 10. Quality Requirements

The repository evidence supports the following measurable quality expectations.

| Quality Area | Requirement |
|--------------|-------------|
| Performance | The backend collector and analyzer functions shall continue to operate on a timer cycle pattern of one minute for collection and 30 seconds later for analysis. |
| Reliability | The analyzer shall use recent queue history and guard conditions to avoid treating transient monitor artifacts as a final queue state. |
| Availability | The system shall expose a health endpoint that reports whether the backend accepts metric collection work and storage access as healthy or degraded. |
| Maintainability | The data access layer shall isolate Azure Table Storage operations behind an IRepository abstraction and a Repository implementation. |
| Scalability | The design shall support multiple monitored queues through table-partitioned storage records and per-queue function execution logic. |
| Usability | The React dashboard shall present multi-queue KPI tiles, queue detail drill-down, and settings controls through a single UI. |
| Security | The production API shall use Azure AD Easy Auth, and the frontend shall acquire Bearer tokens through MSAL. |
| Portability | The system shall run locally on supported development environments and in Azure-hosted deployment scenarios. |
| Interoperability | The system shall integrate Azure Service Bus, Azure Monitor, Azure Table Storage, Microsoft Teams, and SMTP as external services. |
| Testability | The repository includes shell-based scenario tests and structured test result artifacts under the backend test results directories. |
| AI Explainability | To Be Completed |
| AI Safety | To Be Completed |

---

# 11. Performance Requirements

| Requirement | Measurable Target |
|-------------|-------------------|
| Collection cadence | One collector run per monitored queue per minute, aligned to the `:00` timer trigger pattern. |
| Analyzer cadence | One analyzer run per monitored queue approximately 30 seconds after collection. |
| Alert cadence | One alert dispatcher pass per monitored queue approximately 45 seconds after collection. |
| Dashboard response target | The README states the dashboard is expected to answer the three core queue questions in under 10 seconds. |
| Health endpoint behavior | A `status: "Degraded"` result indicates that a collector or analyzer run has not completed within three minutes. |
| Data retention cleanup | Cleanup runs daily at 02:00 UTC and removes rows older than the documented retention windows. |

---

# 12. Assumptions

- The repository is intended to monitor Azure Service Bus queues rather than other messaging systems.
- Azure Monitor rate data may be delayed and therefore is treated as secondary evidence relative to the Service Bus Admin API.
- Azure Table Storage is the selected persistence mechanism for queue configuration, snapshots, status, and alert records.
- Local development may use a storage emulator or a real Azure Storage account, subject to configuration.
- Authentication behavior in production is handled at the Azure Functions host layer, while local development may bypass the login gate when configured.
- The documented qbi-rg, qbi-sb-ns, qbi-queue, and queuebacklogsa names are infrastructure examples and should be treated as current repository references rather than guaranteed production names.

---

# 13. Constraints

| Area | Constraint |
|------|------------|
| Programming language | C# on the .NET 8 backend; JavaScript/React on the frontend |
| Operating system | Local development is documented on macOS, Windows, and Ubuntu; production runs in Azure-managed Linux contexts |
| Database | Azure Table Storage is the repository persistence model |
| Framework | Azure Functions v4 isolated worker on .NET 8; React 18 + Vite frontend |
| Hardware | No dedicated hardware requirement is explicitly documented for the application itself; the host environment must support local development or Azure-hosted execution |
| External APIs | Azure Service Bus Administration API, Azure Monitor Metrics API, Microsoft Teams webhook, SMTP email transport, Azure AD Easy Auth |
| Configuration | Local secrets are stored in `local.settings.json` and must not be committed |

---

# 14. External Interfaces

## User Interfaces

- React dashboard pages for Overview, Queue Detail, and Settings
- Backend health and API responses exposed through the Functions host

## Hardware Interfaces

- To Be Completed

## Software Interfaces

- Azure Functions host runtime
- Azure Table Storage client library
- Azure Service Bus administration client
- Azure Monitor Metrics client
- MSAL authentication flow on the frontend

## Communication Interfaces

- HTTP REST endpoints exposed by DashboardFunction
- Teams webhook POST requests for notification delivery
- SMTP connection to configured email host and port

## External Services

- Azure Service Bus namespace and queue endpoints
- Azure Monitor Metrics service
- Azure Table Storage account
- Microsoft Teams Incoming Webhook or Workflow connector
- SMTP mail server

---

# 15. Requirements Traceability Matrix

| Requirement ID | Level-2 Capability | Requirement Description |
|----------------|--------------------|------------------------|
| FR-1.1.1 | Query Service Bus runtime properties | Collector retrieves Service Bus runtime properties for each configured queue. |
| FR-1.2.1 | Query Azure Monitor metrics | Collector requests incoming and outgoing per-minute metrics where available. |
| FR-1.3.1 | Persist raw queue snapshots | Repository writes the raw snapshot record to the QueueSnapshot table. |
| FR-2.1.1 | Compute queue deltas and trends | Analyzer computes deltas, smoothed rate, and acceleration from queue snapshots. |
| FR-2.2.1 | Classify queue root cause | Analyzer classifies root cause from the computed health state. |
| FR-2.3.1 | Estimate wait time and SLA status | Analyzer evaluates wait time and breach status from queue activity and outgoing rate. |
| FR-3.1.1 | Determine alert severity | Alert Service computes the severity needed for each queue incident. |
| FR-3.2.1 | Dispatch Teams notifications | Alert Service sends a Teams webhook payload when required. |
| FR-3.3.1 | Dispatch email notifications | Alert Service sends an email alert to configured recipients. |
| FR-4.1.1 | Create queue configuration | Dashboard Function creates a new monitored queue configuration. |
| FR-4.2.1 | Update queue configuration | Dashboard Function updates an existing queue configuration. |
| FR-4.3.1 | Delete queue configuration | Dashboard Function removes a monitored queue configuration record. |
| FR-5.1.1 | Return queue summaries | Dashboard Function exposes multi-queue summary data. |
| FR-5.2.1 | Return queue history | Dashboard Function returns historical queue status data. |
| FR-5.3.1 | Return alert history | Dashboard Function returns alert and incident history. |
| FR-6.1.1 | Purge old snapshots | Cleanup Function removes outdated snapshot rows. |
| FR-6.2.1 | Purge old status records | Cleanup Function removes outdated computation rows. |
| FR-6.3.1 | Purge old alert records | Cleanup Function removes outdated alert lifecycle rows. |
| FR-7.1.1 | Protect API access through Azure AD Easy Auth | Production API is protected via Azure AD Easy Auth. |
| FR-7.2.1 | Allow frontend token-based access | Frontend requests include bearer tokens required for API access. |

---

# 16. Future Versions

## Version 1

The current repository implements the first stable operating version of QBIS. The main release scope includes queue metric collection, intelligence classification, alert dispatch, queue configuration management, and dashboard visibility.

## Version 2

To Be Completed.

## Version 3

To Be Completed.

## Future Enhancements

- Additional alert channels and routing options
- More sophisticated dashboard analytics and machine-readable export
- Expanded configuration controls for retention, severity thresholds, and queue-specific integrations
- Improved support for broader Azure service environments beyond the current Service Bus focus

---

# 17. Open Issues

| Issue | Status |
|-------|--------|
| Semester metadata has not been documented in the repository. | To Be Completed |
| Version 2 and Version 3 roadmap details are not yet documented in the repository. | To Be Completed |
| Hardware interface details are not specified in the repository documentation. | To Be Completed |
| AI Explainability and AI Safety requirements are not documented in the repository evidence. | To Be Completed |
| Some operational details such as exact production tenant, namespace, and queue configuration naming remain environment-specific and therefore are not fully generalizable in this PRD. | To Be Completed |

---

# 18. Glossary

| Term | Definition |
|------|------------|
| ActiveCount | Current number of messages in the Service Bus queue. |
| DLQCount | Dead-letter queue message count. |
| QueueSnapshot | Raw metric sample written by the collector for a queue at a point in time. |
| QueueStatus | Computed intelligence result for a queue, including root cause, trend, and severity. |
| AlertRecord | Incident lifecycle record storing alert history and status. |
| SLA | Service-level agreement threshold used to evaluate whether a queue is in breach or at risk. |
| Root Cause | The classified reason for queue degradation, such as ConsumerStopped or ProducerSpike. |
| Trend Label | The short-lived movement classification for queue backlog status, such as GrowingFast or Draining. |
| Sticky Severity | A rule that requires two consecutive OK readings before severity drops, reducing alert flapping. |
| Azure AD Easy Auth | The production API protection model that validates tokens before requests reach the Functions code. |
| MSAL | Microsoft Authentication Library used to acquire tokens in the React frontend. |

---

## Summary

This PRD captures the implementation evidence currently present in the repository. It is intentionally evidence-based and avoids adding unsupported requirements, features, or assumptions. Items that cannot be verified from the repository are marked as To Be Completed so the document remains a living engineering artifact.

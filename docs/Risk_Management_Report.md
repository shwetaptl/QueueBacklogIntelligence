# Risk Management Report
## Queue Backlog Intelligence System (QBIS)
### CISC 594

---

## 1. Project Overview

This Risk Management Report documents the identification, assessment, mitigation, and weekly tracking of project risks for the Queue Backlog Intelligence System (QBIS), the semester project for CISC 594. QBIS is a real-time Azure Service Bus monitoring system built on Azure Functions (.NET 8 / C#) with a React 18 frontend. It monitors queue health every 60 seconds, computes message wait time using a 9-step analyzer pipeline, classifies root causes across 8 categories, dispatches alerts via Microsoft Teams and email, and displays live operational status on a web dashboard.

Seven risks were identified across the project lifecycle. Risks R1–R4 were identified at project inception (Week 1). Risks R5–R7 were identified during development and testing (Weeks 9–10) as new failure modes became apparent through implementation experience. One risk (R2 — Division-by-zero in wait time calculation) was retired in Week 6 after complete resolution. All risks are assessed using Likelihood Score × Consequence Score on a 1–5 scale. Each risk is tracked with both an Initial Risk Score (assessed at identification) and a Net Score (post-mitigation residual risk at Week 11), consistent with the Cash-a-Check risk table format referenced in the assignment. This format extends the Therac-25 risk table with Q1/Q2/Q3 behavioral requirements and functional requirements per risk, providing a more complete risk management record. The full week-by-week history is documented in Section 3.

---

## 2. Risk Summary — Current State (Week 11)

| ID | Undesirable Event | Init L×C | Init Score | Net Score | Level | Status |
|----|-------------------|----------|-----------|-----------|-------|--------|
| **R1** | Azure Monitor ingestion delay causes incorrect state classification | 4×4=16 | **16** | **9** | Medium | Active — mitigated |
| **R2** | Division-by-zero in Little's Law wait time formula | — | **RETIRED** | **0 — RETIRED** | RETIRED | RETIRED Week 6 — resolved |
| **R3** | Teams webhook payload silently rejected | 3×5=15 | **15** | **6** | Medium | Active — mitigated |
| **R4** | Dashboard scope creep | 3×3=9 | **9** | **2** | Low | Active — near resolved |
| **R5** | Teams webhook URL pattern detection brittle | 2×4=8 | **8** | **8** | Medium | Active — monitoring |
| **R6** | New queue shows UNKNOWN for first 1–2 cycles | 5×2=10 | **10** | **10** | Medium | Active — accepted |
| **R7** | Dashboard 30s polling delay during fast incidents | 4×2=8 | **8** | **8** | Medium | Active — accepted |
| **R8** | Azure AD Easy Auth failure in production | 2×5=10 | **10** | **8** | Medium | Active — verified, accepted |

**Score Legend:** Red ≥ 16 (Critical) | Amber 8–15 (High/Medium) | Green < 8 (Low) | Grey = Retired

**Init L×C** = Initial Risk Score at identification | **Net Score** = Post-mitigation residual risk score at Week 11

---

## 3. Week-by-Week Risk Score History

The following table shows the risk score (Likelihood × Consequence) for each risk across all 11 weeks of the project. Dashes (—) indicate the risk had not yet been identified. RETIRED indicates the risk was resolved and closed.

| Week | Date | R1 | R2 | R3 | R4 | R5 | R6 | R7 | R8 | Milestone / Notes |
|------|------|----|----|----|----|----|----|----|----|-------------------|
| W1 | May 9, 2026 | 4×4=16 | 3×5=15 | 3×5=15 | 3×3=9 | — | — | — | — | Project kickoff. V1 architecture defined. Initial risks R1–R4 identified. |
| W2 | May 16, 2026 | 4×4=16 | 3×5=15 | 3×5=15 | 3×3=9 | — | — | — | — | V1 development begins. CollectorFunction implemented. R2 mitigation in progress. |
| W3 | May 23, 2026 | 4×4=16 | 3×5=15 | 3×5=15 | 3×3=9 | — | — | — | — | AnalyzerService 9-step pipeline implemented (Steps 1–6). R1 ConsistencyTolerance defined. |
| W4 | May 30, 2026 | 4×4=16 | 2×5=10 | 2×5=10 | 3×3=9 | — | — | — | — | Analyzer Steps 7–9 complete. Alert severity and NeedToSendAlert logic implemented. |
| W5 | Jun 6, 2026 | 4×3=12 | 2×5=10 | 2×5=10 | 2×3=6 | — | — | — | — | V1 system testing begins. TC01–TC04 executed. R2 nearly resolved. |
| W6 | Jun 13, 2026 | 4×3=12 | RETIRED | 2×4=8 | 2×3=6 | — | — | — | — | v1.0.0 released. R2 RETIRED — division-by-zero fully resolved. TC01–TC05 passed. |
| W7 | Jun 20, 2026 | 3×3=9 | RETIRED | 2×4=8 | 2×2=4 | — | — | — | — | V2 begins. AlertDispatcherFunction implemented. v1.1.0 Teams dual-format released. |
| W8 | Jun 27, 2026 | 3×3=9 | RETIRED | 2×3=6 | 2×2=4 | — | — | — | — | React dashboard V1 complete. TC06–TC08 executed. Dashboard feature freeze applied. |
| W9 | Jul 7, 2026 | 3×3=9 | RETIRED | 2×3=6 | 1×2=2 | 2×4=8 | 5×2=10 | — | 2×5=10 | Azure AD auth added (v1.3.0). R5, R6, R8 identified as new risks. |
| W10 | Jul 13, 2026 | 3×3=9 | RETIRED | 2×3=6 | 1×2=2 | 2×4=8 | 5×2=10 | 4×2=8 | 2×4=8 | CleanupFunction added (v1.4.0). R7 identified. TC09–TC10 executed. |
| W11 | Jul 21, 2026 | 3×3=9 | RETIRED | 2×3=6 | 1×2=2 | 2×4=8 | 5×2=10 | 4×2=8 | 2×4=8 | v1.4.1 released. 11 test scenarios executed. Risk report submitted. |

---

## 4. Detailed Risk Assessments

The following sections provide the full risk assessment for each of the eight identified risks, including the undesirable event, risk description, likelihood and consequence justifications, mitigation plan, Q1/Q2/Q3 behavioral requirements, functional requirements, and the complete week-by-week score history for that risk.

---

### R1: Azure Monitor ingestion delay causes incorrect state classification

**Capability:** Queue State Classification  
**Current Status:** Active

| Field | Details |
|-------|---------|
| **Undesirable Event** | Azure Monitor ingestion delay causes incorrect state classification |
| **Risk Description** | Azure Monitor metrics (IncomingPerMin, OutgoingPerMin) have a 2–4 minute ingestion delay. If the Analyzer uses stale Monitor data as ground truth, it may misclassify queue state — for example, reading a zero outgoing rate when consumers are actually processing — producing incorrect wait time calculations and false SLA breach alerts. |
| **Initial Likelihood Score** | 4 / 5 (at identification) |
| **Likelihood Justification** | Azure Monitor's ingestion delay is a documented platform behavior that occurs consistently on every analysis cycle. It affects the first 2–4 minutes of every new measurement window and is present throughout the system's operational life. |
| **Initial Consequence Score** | 4 / 5 (at identification) |
| **Consequence Justification** | Incorrect state classification propagates through all 9 analyzer pipeline steps, producing wrong wait times, wrong root cause labels, wrong severity assessments, and potentially false alerts to engineering teams — undermining trust in the system. |
| **Initial Risk Score (L × C)** | 4 × 4 = 16 |
| **Mitigation** | Software mitigation: ActiveCount delta from the Service Bus Admin API (zero-delay) is used as primary ground truth for all state classification decisions. Monitor-sourced rates are treated as secondary and validated against ActiveCount delta before use. Null is stored rather than zero when Monitor data is unavailable, preventing misinterpretation. A 3-snapshot weighted average (weights: 0.50, 0.30, 0.20) smooths transient Monitor artifacts. |
| **Current Likelihood / Consequence** | 3 / 5 × 3 / 5 (Week 11) |
| **Net Score (Post-Mitigation)** | 3 × 3 = 9 |
| **Net Score Justification** | Post-mitigation residual score (Week 11). ActiveCount-first design, weighted smoothing, and null-storage mitigation reduced consequence from 4 to 3 and likelihood from 4 to 3. |

| Q1 — Desired Behavior | Q2 — Preventative Behavior | Q3 — Responsive Behavior |
|-----------------------|---------------------------|--------------------------|
| The Analyzer shall classify queue state every 60 seconds using ActiveCount delta as primary ground truth. It shall compute wait time, trend label, root cause, and alert severity based on the most reliable available data for each cycle. | The Analyzer shall not use Monitor-sourced IncomingPerMin or OutgoingPerMin as the sole basis for state classification. It shall not store zero as the outgoing rate when Monitor data is absent. It shall not produce a wait time calculation when the outgoing rate is null. | When Monitor data is unavailable or inconsistent with ActiveCount delta beyond the ConsistencyTolerance threshold (5× noiseFloor), the Analyzer shall fall back to ActiveCount-derived rate estimation and record SlaStatus as UNKNOWN rather than fabricating a wait time value. |

| Functional Requirements (Q1) | Preventative Requirements (Q2) | Responsive Requirements (Q3) |
|------------------------------|-------------------------------|------------------------------|
| FR-R1.1: AnalyzerService shall read ActiveCount and DLQCount from QueueSnapshot (Admin API, zero-delay) before reading IncomingPerMin and OutgoingPerMin. FR-R1.2: AnalyzerService shall compute netRateNow and netRateSmoothed from ActiveCount deltas across consecutive snapshots. FR-R1.3: AnalyzerService shall validate Monitor rates against ActiveCount delta using ConsistencyTolerance before accepting them. FR-R1.4: AnalyzerService shall apply weighted smoothing (0.50/0.30/0.20) across the last 3 Monitor rate readings. | FR-R1.5: AnalyzerService shall not classify queue state as ConsumerStopped, Growing, or Draining based solely on Monitor-sourced rates. FR-R1.6: CollectorFunction shall store null (not zero) for IncomingPerMin and OutgoingPerMin when Azure Monitor returns no data. | FR-R1.7: AnalyzerService shall set SlaStatus to UNKNOWN when OutgoingRate is null and the queue is not in an Idle or Empty state. FR-R1.8: AnalyzerService shall log a data quality warning when Monitor data is absent for 3 or more consecutive cycles. |

**Weekly Score History — R1**

| Week | Date | L × C = Score | Notes / Changes |
|------|------|--------------|-----------------|
| W1 | May 9, 2026 | 4 × 4 = **16** | Identified as primary technical risk. ActiveCount-first design decision made. |
| W2 | May 16, 2026 | 4 × 4 = **16** | Implementing 3-snapshot weighted average smoothing to mitigate Monitor lag. |
| W3 | May 23, 2026 | 4 × 4 = **16** | ConsistencyTolerance threshold defined (5× noiseFloor). Under development. |
| W4 | May 30, 2026 | 4 × 4 = **16** | Null-storage for missing Monitor data implemented in CollectorFunction. |
| W5 | Jun 6, 2026 | 4 × 3 = **12** | Weighted average and fallback logic validated against TC01 and TC02. Consequence reduced. |
| W6 | Jun 13, 2026 | 4 × 3 = **12** | R2 retired this week. R1 mitigation holding. No false alerts observed. |
| W7 | Jun 20, 2026 | 3 × 3 = **9** | TC04 Stable Balanced Queue passed. Monitor lag handled correctly. Likelihood reduced. |
| W8 | Jun 27, 2026 | 3 × 3 = **9** | All 9 analyzer pipeline steps validated. Monitor delay mitigation confirmed effective. |
| W9 | Jul 7, 2026 | 3 × 3 = **9** | No Monitor-lag-related misclassifications observed in 3 weeks of testing. |
| W10 | Jul 13, 2026 | 3 × 3 = **9** | Risk stable. Remaining likelihood reflects inherent Azure platform behavior beyond our control. |
| W11 | Jul 21, 2026 | 3 × 3 = **9** | Current state. Risk accepted — ActiveCount-first design provides adequate mitigation. |

---

### R2: Division-by-zero in Little's Law wait time formula [RETIRED]

**Capability:** Wait Time Calculation  
**Current Status:** RETIRED — Week 6

| Field | Details |
|-------|---------|
| **Undesirable Event** | Division-by-zero in Little's Law wait time formula |
| **Risk Description** | The wait time formula WaitTimeMinutes = ActiveCount / OutgoingPerMin produces an undefined result when OutgoingPerMin is zero. This occurs in ConsumerStopped and Idle states. If not handled, division by zero causes a runtime exception that crashes the Analyzer for that queue cycle, producing no QueueStatus output and disabling all downstream alerting. |
| **Initial Likelihood Score** | 3 / 5 (at identification) |
| **Likelihood Justification** | ConsumerStopped and Idle states are explicitly expected operational states. Division by zero is triggered any time a consumer stops processing or the queue is idle — both normal and incident scenarios. |
| **Initial Consequence Score** | 5 / 5 (at identification) |
| **Consequence Justification** | An unhandled division-by-zero crashes the entire Analyzer cycle for the affected queue. No QueueStatus is written, no alert is dispatched, and the dashboard shows stale data — effectively blinding the monitoring system during the exact incident scenario it exists to detect. |
| **Initial Risk Score (L × C)** | 3 × 5 = 15 |
| **Mitigation** | Software mitigation: AnalyzerService Step 4 explicitly detects zero-rate states (Idle, ConsumerStopped) before attempting wait time calculation. For these states, OutgoingRate is set to zero and WaitTimeMinutes is set to null. SlaStatus is set to BREACHING for ConsumerStopped and OK for Idle. No division is performed. This is enforced as a state-specific branch in Step 5, not a try/catch. RESOLVED Week 6. |
| **Current Likelihood / Consequence** | RETIRED — N/A |
| **Net Score (Post-Mitigation)** | 0 — RETIRED |
| **Net Score Justification** | RETIRED Week 6. Risk fully resolved. Net Score = 0 — no residual risk remains. |

| Q1 — Desired Behavior | Q2 — Preventative Behavior | Q3 — Responsive Behavior |
|-----------------------|---------------------------|--------------------------|
| The Analyzer shall compute WaitTimeMinutes for all queue states where OutgoingRate is reliably non-zero and sufficient data exists. | The Analyzer shall not perform division when OutgoingRate is zero or null. It shall not produce a numerical WaitTimeMinutes value for ConsumerStopped or Idle states. | When the queue is in ConsumerStopped state, the Analyzer shall set WaitTimeMinutes to null and SlaStatus to BREACHING. When the queue is Idle, the Analyzer shall set WaitTimeMinutes to 0 and SlaStatus to OK. In both cases, no division operation shall be performed. |

| Functional Requirements (Q1) | Preventative Requirements (Q2) | Responsive Requirements (Q3) |
|------------------------------|-------------------------------|------------------------------|
| FR-R2.1: AnalyzerService Step 5 shall check queue state before computing WaitTimeMinutes. FR-R2.2: AnalyzerService shall set WaitTimeMinutes = null for ConsumerStopped state. FR-R2.3: AnalyzerService shall set WaitTimeMinutes = 0 for Idle or Empty state. | FR-R2.4: AnalyzerService shall not execute division when OutgoingRate == 0 or OutgoingRate == null. FR-R2.5: AnalyzerService shall not derive WaitTimeMinutes from Monitor rates alone when ActiveCount indicates ConsumerStopped. | FR-R2.6: AnalyzerService shall set SlaStatus = BREACHING for ConsumerStopped regardless of WaitTimeMinutes being null. FR-R2.7: AnalyzerService shall set SlaStatus = OK for Idle state with WaitTimeMinutes = 0. |

**Weekly Score History — R2**

| Week | Date | L × C = Score | Notes / Changes |
|------|------|--------------|-----------------|
| W1 | May 9, 2026 | 3 × 5 = **15** | Identified during design review. Division-by-zero in ConsumerStopped/Idle states. |
| W2 | May 16, 2026 | 3 × 5 = **15** | State-specific branch design agreed. Implementation in progress. |
| W3 | May 23, 2026 | 3 × 5 = **15** | Step 4 (Derive Reliable Outgoing Rate) implementation includes zero-rate detection. |
| W4 | May 30, 2026 | 2 × 5 = **10** | Zero-rate state detection coded. Unit test written. Awaiting full validation. |
| W5 | Jun 6, 2026 | 2 × 5 = **10** | TC01 (Empty Queue) and TC08 (Idle with stale messages) both passed. Nearly resolved. |
| W6 | Jun 13, 2026 | **RETIRED** | RETIRED. Division-by-zero fully resolved. ConsumerStopped → null/BREACHING, Idle → 0/OK. TC02 confirmed. No further tracking needed. |
| W7–W11 | — | **RETIRED** | RETIRED |

---

### R3: Teams webhook payload silently rejected with no error response

**Capability:** Alert Dispatch  
**Current Status:** Active

| Field | Details |
|-------|---------|
| **Undesirable Event** | Teams webhook payload silently rejected with no error response |
| **Risk Description** | Microsoft Teams incoming webhook endpoints silently reject malformed payloads — returning HTTP 200 OK with body 'Summary or Text is required' or similar, with no exception raised in the caller. If the Adaptive Card or MessageCard JSON structure is incorrect, AlertService.SendTeamsAlertAsync() reports success while no alert is actually delivered to the engineering team. |
| **Initial Likelihood Score** | 3 / 5 (at identification) |
| **Likelihood Justification** | Teams has two webhook formats (legacy MessageCard for webhook.office.com URLs and Adaptive Card for Workflows connector URLs) that require different JSON structures. Format is detected by URL pattern at runtime. Any format mismatch or payload structure error causes silent rejection. |
| **Initial Consequence Score** | 5 / 5 (at identification) |
| **Consequence Justification** | Silent alert failure during a live SLA breach means engineers receive no notification. The entire value proposition of QBIS — alerting teams to incidents — fails completely and invisibly. The pipeline continues running and reporting success while engineers receive nothing. |
| **Initial Risk Score (L × C)** | 3 × 5 = 15 |
| **Mitigation** | Software mitigation: AlertService detects webhook format at runtime by inspecting the URL (webhook.office.com → legacy MessageCard; all others → Adaptive Card). Both formats are prototyped and validated against a live Teams channel before production deployment. HTTP response body is parsed and logged even when status is 200 — 'Summary or Text is required' triggers a logged error. A dedicated test scenario validates the full alert path end-to-end. |
| **Current Likelihood / Consequence** | 2 / 5 × 3 / 5 (Week 11) |
| **Net Score (Post-Mitigation)** | 2 × 3 = 6 |
| **Net Score Justification** | Post-mitigation residual score (Week 11). Dual-format validation, response body parsing, and retry logic reduced likelihood from 3 to 2 and consequence from 5 to 3. |

| Q1 — Desired Behavior | Q2 — Preventative Behavior | Q3 — Responsive Behavior |
|-----------------------|---------------------------|--------------------------|
| The AlertService shall send correctly formatted Teams notifications for all alert types (NewIncident, Escalation, Reminder, Recovery) using the webhook format appropriate for the configured URL. | The AlertService shall not report alert success when the Teams API response body contains an error indicator. It shall not assume HTTP 200 means successful delivery. | When the Teams response body indicates an error (non-empty error string or known rejection pattern), the AlertService shall log the failure with the full response body, set NeedToSendAlert to true for retry on the next cycle, and emit a health-check warning. |

| Functional Requirements (Q1) | Preventative Requirements (Q2) | Responsive Requirements (Q3) |
|------------------------------|-------------------------------|------------------------------|
| FR-R3.1: AlertService shall inspect the webhook URL and select MessageCard format for webhook.office.com URLs and Adaptive Card format for all other URLs. FR-R3.2: AlertService shall include all required fields for each format (Summary for MessageCard; body.type='AdaptiveCard' for Adaptive Card). FR-R3.3: AlertService shall validate both webhook formats against a live Teams channel before each version deployment. | FR-R3.4: AlertService shall parse the HTTP response body for all Teams webhook calls regardless of HTTP status code. FR-R3.5: AlertService shall treat any response body containing known Teams rejection strings as a delivery failure. FR-R3.6: AlertService shall not update AlertRecord with LastAlertSentUtc if Teams delivery failed. | FR-R3.7: AlertService shall log Teams webhook failures with full response body at Error level. FR-R3.8: AlertService shall preserve NeedToSendAlert=true in QueueStatus when Teams delivery fails, enabling retry on the next Dispatcher cycle. |

**Weekly Score History — R3**

| Week | Date | L × C = Score | Notes / Changes |
|------|------|--------------|-----------------|
| W1 | May 9, 2026 | 3 × 5 = **15** | Identified as high-impact alert delivery risk. Teams dual-format requirement documented. |
| W2 | May 16, 2026 | 3 × 5 = **15** | MessageCard format prototyped and validated in Teams test channel (webhook.office.com). |
| W3 | May 23, 2026 | 3 × 5 = **15** | Adaptive Card format (Workflows connector) prototyped. Both formats validated. |
| W4 | May 30, 2026 | 2 × 5 = **10** | Runtime format detection by URL pattern implemented. Response body parsing added. |
| W5 | Jun 6, 2026 | 2 × 5 = **10** | Full alert lifecycle tested: NewIncident → Escalation → Reminder → Recovery all validated. |
| W6 | Jun 13, 2026 | 2 × 4 = **8** | AlertRecord lifecycle confirmed correct. Consequence reduced — retry logic operational. |
| W7 | Jun 20, 2026 | 2 × 4 = **8** | v1.1.0 release: Teams dual-format confirmed working. No silent rejection observed in testing. |
| W8 | Jun 27, 2026 | 2 × 3 = **6** | TC05b (Auto Recovery) and TC10 (Full Lifecycle) both passed alert validation. |
| W9 | Jul 7, 2026 | 2 × 3 = **6** | Risk stable. Residual risk is Microsoft changing Teams webhook URL patterns (tracked as R5). |
| W10 | Jul 13, 2026 | 2 × 3 = **6** | No Teams delivery failures observed. Risk score stable. |
| W11 | Jul 21, 2026 | 2 × 3 = **6** | Current state. Payload format validated. Retry logic tested. Risk accepted at current level. |

---

### R4: Dashboard feature creep extends development beyond planned scope

**Capability:** Frontend Dashboard  
**Current Status:** Active

| Field | Details |
|-------|---------|
| **Undesirable Event** | Dashboard feature creep extends development beyond planned scope |
| **Risk Description** | Dashboard development can expand beyond planned effort as additional display features, chart types, or UI refinements are considered during implementation. Uncontrolled scope expansion risks delaying V2 delivery, reducing time available for system testing, and introducing untested UI complexity that obscures testing results. |
| **Initial Likelihood Score** | 3 / 5 (at identification) |
| **Likelihood Justification** | Frontend scope creep is a common risk in any iterative UI development. Real-time data visualization with time-range selection, multiple chart types, and incident history tables creates many opportunities for incremental feature additions that individually seem minor but cumulatively consume significant development time. |
| **Initial Consequence Score** | 3 / 5 (at identification) |
| **Consequence Justification** | Scope creep delays V2 system testing. Since the assignment requires a complete system test after each version, reduced testing time directly affects testing quality and the ability to validate all 10 test scenarios. Late dashboard delivery also compresses time available for documentation. |
| **Initial Risk Score (L × C)** | 3 × 3 = 9 |
| **Mitigation** | The dashboard is strictly scoped to five components: StatusRow (4 KPI tiles), ActiveCountChart (dual-panel time-series), RootCauseTimeline (color-coded strip), IncidentTable (sortable incident history), and Sidebar (navigation + health dot). No additional components are added without explicit re-approval as part of the defined change control process. All new feature requests are logged as GitHub Issues with 'scope-change' label and require milestone review before implementation. |
| **Current Likelihood / Consequence** | 1 / 5 × 2 / 5 (Week 11) |
| **Net Score (Post-Mitigation)** | 1 × 2 = 2 |
| **Net Score Justification** | Post-mitigation residual score (Week 11). Component scope lock and feature freeze reduced likelihood from 3 to 1 and consequence from 3 to 2. |

| Q1 — Desired Behavior | Q2 — Preventative Behavior | Q3 — Responsive Behavior |
|-----------------------|---------------------------|--------------------------|
| The dashboard shall display current queue health (wait time, trend, root cause, severity), time-series charts, root cause timeline, and incident history for all monitored queues. | The dashboard shall not implement features beyond the defined component scope without explicit scope change approval. It shall not add new chart types, data filters, or UI widgets that are not in the approved component list. | When a new feature request is raised during development, the dashboard implementation shall log it as a GitHub Issue with 'scope-change' label, defer it to post-submission review, and continue with the approved scope without interruption. |

| Functional Requirements (Q1) | Preventative Requirements (Q2) | Responsive Requirements (Q3) |
|------------------------------|-------------------------------|------------------------------|
| FR-R4.1: Frontend shall implement exactly the five approved components: StatusRow, ActiveCountChart, RootCauseTimeline, IncidentTable, Sidebar. FR-R4.2: Each component shall have a defined set of props and no undocumented side effects. | FR-R4.3: Frontend shall not implement any component not in the approved list without a GitHub Issue tagged 'scope-change' being reviewed and approved. FR-R4.4: Frontend build shall not include unused component files or experimental features. | FR-R4.5: When a new feature is requested mid-development, the developer shall create a GitHub Issue, add the 'scope-change' label, and document the request without implementing it until reviewed. FR-R4.6: The milestone review shall occur at the start of each development week to assess and approve or defer outstanding scope-change requests. |

**Weekly Score History — R4**

| Week | Date | L × C = Score | Notes / Changes |
|------|------|--------------|-----------------|
| W1 | May 9, 2026 | 3 × 3 = **9** | Identified during V2 planning. Dashboard component list defined and locked. |
| W2 | May 16, 2026 | 3 × 3 = **9** | React project scaffolded. Sidebar and Overview page only. Scope boundary holding. |
| W3 | May 23, 2026 | 3 × 3 = **9** | QueueDetail page started. TimeRangeSelector added — within approved scope. |
| W4 | May 30, 2026 | 3 × 3 = **9** | ActiveCountChart dual-panel implemented. Recharts integration complete. |
| W5 | Jun 6, 2026 | 2 × 3 = **6** | RootCauseTimeline and IncidentTable complete. Scope boundary held. Likelihood reduced. |
| W6 | Jun 13, 2026 | 2 × 3 = **6** | v1.2.0 frontend redesign (Sidebar + multi-queue Overview) delivered on scope. |
| W7 | Jun 20, 2026 | 2 × 2 = **4** | v1.3.0 Azure AD auth added — planned feature, not scope creep. Consequence reduced. |
| W8 | Jun 27, 2026 | 2 × 2 = **4** | All 5 approved components implemented and tested. Dashboard feature freeze applied. |
| W9 | Jul 7, 2026 | 1 × 2 = **2** | No new feature requests pending. Scope boundary maintained for 4 consecutive weeks. |
| W10 | Jul 13, 2026 | 1 × 2 = **2** | Risk nearly resolved. Final dashboard validated against all test scenarios. |
| W11 | Jul 21, 2026 | 1 × 2 = **2** | Current state. Risk accepted. Feature freeze in effect. No new scope additions planned. |

---

### R5: Microsoft changes Teams webhook URL patterns, breaking format detection

**Capability:** Alert Dispatch  
**Current Status:** Active

| Field | Details |
|-------|---------|
| **Undesirable Event** | Microsoft changes Teams webhook URL patterns, breaking format detection |
| **Risk Description** | AlertService detects whether to use legacy MessageCard or Adaptive Card format by inspecting the webhook URL — specifically, whether it contains 'webhook.office.com'. If Microsoft changes the URL structure for either webhook type (as they have done historically when introducing the Workflows connector), format detection silently fails and the wrong payload format is sent, resulting in rejected alerts. |
| **Initial Likelihood Score** | 2 / 5 (at identification) |
| **Likelihood Justification** | Microsoft has already changed Teams webhook infrastructure once (introducing Workflows connector alongside the legacy incoming webhook), motivating the dual-format design. A second change is plausible over the system's operational life but not imminent based on current Microsoft documentation. |
| **Initial Consequence Score** | 4 / 5 (at identification) |
| **Consequence Justification** | Format detection failure causes all Teams alerts to be silently rejected with HTTP 200 responses — the same failure mode as R3 but now systematic across all alert types rather than caused by payload errors. Engineers receive no notifications during SLA breaches. |
| **Initial Risk Score (L × C)** | 2 × 4 = 8 |
| **Mitigation** | Software mitigation: AlertService logs the detected format ('Using Adaptive Card format' or 'Using MessageCard format') at Info level for every alert sent. The detection condition (URL.Contains('webhook.office.com')) is isolated in a single method (DetectWebhookFormat) that can be updated in one place without touching alert formatting logic. Alert delivery health is monitored via response body parsing — any systematic failure triggers a health-check warning. The webhook URL is stored in QueueConfig and can be updated without redeployment. |
| **Current Likelihood / Consequence** | 2 / 5 × 4 / 5 (Week 11) |
| **Net Score (Post-Mitigation)** | 2 × 4 = 8 |
| **Net Score Justification** | Post-mitigation residual score (Week 11). DetectWebhookFormat isolation and health monitoring reduce impact but cannot eliminate the external platform dependency. Score unchanged — residual risk accepted. |

| Q1 — Desired Behavior | Q2 — Preventative Behavior | Q3 — Responsive Behavior |
|-----------------------|---------------------------|--------------------------|
| AlertService shall correctly detect and apply the Teams webhook format appropriate for the configured URL on every alert dispatch. | AlertService shall not hardcode webhook format selection. It shall not assume a single Teams webhook URL format is valid for all configured webhook URLs. | When AlertService detects a consistent pattern of Teams alert delivery failures (response body errors on 3 or more consecutive cycles), it shall emit a Critical health-check warning, log the detected format and URL pattern, and flag the issue for manual investigation. |

| Functional Requirements (Q1) | Preventative Requirements (Q2) | Responsive Requirements (Q3) |
|------------------------------|-------------------------------|------------------------------|
| FR-R5.1: AlertService shall isolate webhook format detection in a single method (DetectWebhookFormat) that accepts the URL and returns the format enum. FR-R5.2: AlertService shall log the selected format at Info level for every Teams alert dispatch. | FR-R5.3: AlertService shall not select webhook format based on any logic outside DetectWebhookFormat. FR-R5.4: AlertService shall not suppress HTTP response body content from Teams webhook calls. | FR-R5.5: AlertService shall count consecutive Teams delivery failures per queue and emit a Critical health warning after 3 consecutive failures. FR-R5.6: GET /api/health endpoint shall include last successful Teams alert timestamp to enable detection of systematic failures. |

**Weekly Score History — R5**

| Week | Date | L × C = Score | Notes / Changes |
|------|------|--------------|-----------------|
| W1–W8 | May 9 – Jun 27, 2026 | Not yet identified | — |
| W9 | Jul 7, 2026 | 2 × 4 = **8** | Added Week 9 after v1.1.0 Teams dual-format implementation. URL-pattern detection identified as brittle dependency. |
| W10 | Jul 13, 2026 | 2 × 4 = **8** | DetectWebhookFormat method isolated. Logging added. Risk score stable. |
| W11 | Jul 21, 2026 | 2 × 4 = **8** | Current state. Mitigation implemented. Risk accepted — dependent on Microsoft platform stability. |

---

### R6: New queue shows UNKNOWN state for first 1–2 analyzer cycles

**Capability:** Queue State Detection  
**Current Status:** Active

| Field | Details |
|-------|---------|
| **Undesirable Event** | New queue shows UNKNOWN state for first 1–2 analyzer cycles |
| **Risk Description** | The Analyzer requires at least 2 consecutive QueueSnapshot rows to compute ActiveCount deltas and at least 5–15 minutes of history to establish baseline rates for root cause classification. On a newly configured queue, the first 1–2 analyzer cycles (60–120 seconds) produce SlaStatus=UNKNOWN and RootCause=Unknown, which may cause false alert suppression or confuse operators who expect immediate status visibility. |
| **Initial Likelihood Score** | 5 / 5 (at identification) |
| **Likelihood Justification** | This is a deterministic limitation — it occurs for every newly configured queue without exception. The first analyzer cycle after a queue is added always lacks the required snapshot history and always produces UNKNOWN state. |
| **Initial Consequence Score** | 2 / 5 (at identification) |
| **Consequence Justification** | The consequence is limited because the UNKNOWN period lasts at most 1–2 minutes and the dashboard clearly shows UNKNOWN state rather than false OK. Engineers adding a new queue expect a brief initialization period. No false alerts are triggered and no data is corrupted. |
| **Initial Risk Score (L × C)** | 5 × 2 = 10 |
| **Mitigation** | Software mitigation: AlertSeverity Step 8 treats UNKNOWN SlaStatus as non-alerting (Severity=None) unless RootCause indicates a definitive breach condition. The dashboard displays UNKNOWN with a visual indicator distinct from OK and BREACHING. Documentation (L04 in Known Limitations) explicitly states this behavior. No functional change is planned — the 1–2 minute initialization window is accepted as a known and communicated limitation. |
| **Current Likelihood / Consequence** | 5 / 5 × 2 / 5 (Week 11) |
| **Net Score (Post-Mitigation)** | 5 × 2 = 10 |
| **Net Score Justification** | Post-mitigation residual score (Week 11). Likelihood remains 5 (deterministic behavior on every new queue). Consequence remains 2 (limited impact — accepted as documented limitation L04). Score unchanged — residual risk accepted. |

| Q1 — Desired Behavior | Q2 — Preventative Behavior | Q3 — Responsive Behavior |
|-----------------------|---------------------------|--------------------------|
| The Analyzer shall produce a valid SlaStatus of OK, BREACHING, or UNKNOWN for every queue on every cycle, including the first cycle after queue configuration. | The Analyzer shall not produce alertable severity during UNKNOWN state unless a definitive breach condition is detected from available data. It shall not display UNKNOWN state as OK or BREACHING in the dashboard. | When SlaStatus is UNKNOWN, the Analyzer shall set AlertSeverity to None, log the reason (insufficient history), and set NeedToSendAlert to false to prevent false alerts during initialization. |

| Functional Requirements (Q1) | Preventative Requirements (Q2) | Responsive Requirements (Q3) |
|------------------------------|-------------------------------|------------------------------|
| FR-R6.1: AnalyzerService shall return SlaStatus=UNKNOWN when insufficient snapshot history exists to compute a reliable outgoing rate. FR-R6.2: Dashboard shall display UNKNOWN state with a distinct visual indicator (grey) separate from OK (green) and BREACHING (red/amber). | FR-R6.3: AnalyzerService shall not set AlertSeverity to Warning or Critical when SlaStatus is UNKNOWN due to insufficient history. FR-R6.4: AlertDispatcherFunction shall not send a Teams or email notification when SlaStatus is UNKNOWN and RootCause is Unknown. | FR-R6.5: AnalyzerService shall log 'Insufficient snapshot history for [queue name] — returning UNKNOWN' at Debug level for cycles where history is below minimum threshold. FR-R6.6: AnalyzerService shall transition from UNKNOWN to a computed state within 2 analyzer cycles (120 seconds) of queue configuration. |

**Weekly Score History — R6**

| Week | Date | L × C = Score | Notes / Changes |
|------|------|--------------|-----------------|
| W1–W8 | May 9 – Jun 27, 2026 | Not yet identified | — |
| W9 | Jul 7, 2026 | 5 × 2 = **10** | Added Week 9 during TC01 baseline testing. First-cycle UNKNOWN behavior documented. |
| W10 | Jul 13, 2026 | 5 × 2 = **10** | Confirmed: UNKNOWN state lasts exactly 1–2 cycles. No false alerts. Accepted as known limitation (L04). |
| W11 | Jul 21, 2026 | 5 × 2 = **10** | Current state. Risk accepted. Dashboard displays UNKNOWN correctly. No mitigation beyond documentation planned. |

---

### R7: Dashboard polling delay causes stale status display during fast-moving incidents

**Capability:** Real-Time Dashboard  
**Current Status:** Active

| Field | Details |
|-------|---------|
| **Undesirable Event** | Dashboard polling delay causes stale status display during fast-moving incidents |
| **Risk Description** | The React dashboard polls queue status every 30 seconds and alert history every 60 seconds. No WebSocket or Server-Sent Events real-time push is implemented. During a fast-developing incident — such as a consumer crash with rapidly growing backlog — the dashboard can display data that is 30–60 seconds out of date relative to the current QueueStatus in Table Storage, potentially showing a lower severity than the Analyzer has already computed. |
| **Initial Likelihood Score** | 4 / 5 (at identification) |
| **Likelihood Justification** | The 30-second polling interval is a fixed architectural decision documented in Known Limitations (L01). Every fast-developing incident will exhibit this delay. The delay is guaranteed to occur — only its operational impact varies. |
| **Initial Consequence Score** | 2 / 5 (at identification) |
| **Consequence Justification** | The consequence is limited because alert notifications (Teams and email) are dispatched by AlertDispatcherFunction within 45 seconds of the Analyzer computing a breach — independently of dashboard polling. Engineers receive the alert through Teams/email before they see it on the dashboard. The dashboard delay is a display latency issue, not an alerting latency issue. |
| **Initial Risk Score (L × C)** | 4 × 2 = 8 |
| **Mitigation** | Software mitigation: The dashboard displays a 'Last updated' timestamp on each panel, enabling engineers to assess data freshness. The Sidebar health dot polls /api/health every 60 seconds and turns red if the Analyzer has not run in over 3 minutes, providing a system health indicator. The useFetch hook clears stale data immediately on URL change, preventing display of stale data after navigation. No WebSocket implementation is planned — the polling architecture is accepted as appropriate for the current deployment model. |
| **Current Likelihood / Consequence** | 4 / 5 × 2 / 5 (Week 11) |
| **Net Score (Post-Mitigation)** | 4 × 2 = 8 |
| **Net Score Justification** | Post-mitigation residual score (Week 11). Last-updated timestamp and health dot mitigate confusion but polling architecture remains. Score unchanged — residual risk accepted. Alert delivery via Teams/email is unaffected. |

| Q1 — Desired Behavior | Q2 — Preventative Behavior | Q3 — Responsive Behavior |
|-----------------------|---------------------------|--------------------------|
| The dashboard shall display the most recently available QueueStatus data, refreshed at regular polling intervals, with a visible last-updated timestamp. | The dashboard shall not display cached data from a previous polling cycle without indicating its age. It shall not show OK status when the displayed timestamp is more than 60 seconds old. | When a polling request fails or returns stale data (timestamp older than 90 seconds), the dashboard shall display a visual staleness warning and prompt the user to refresh manually. |

| Functional Requirements (Q1) | Preventative Requirements (Q2) | Responsive Requirements (Q3) |
|------------------------------|-------------------------------|------------------------------|
| FR-R7.1: useFetch hook shall display the fetchedAt timestamp alongside all polled data. FR-R7.2: StatusRow component shall show a 'Last updated X seconds ago' indicator updated every second via useSecondsAgo. | FR-R7.3: Dashboard shall not display data from a polling cycle older than 90 seconds without a visual staleness indicator. FR-R7.4: Dashboard shall not suppress the fetchedAt timestamp or last-updated indicator from any data panel. | FR-R7.5: When a fetch returns an error or the fetchedAt timestamp exceeds 90 seconds, dashboard shall display a yellow warning banner: 'Data may be stale — last updated [timestamp].' FR-R7.6: Sidebar health dot shall turn red when /api/health reports collectorDelayMinutes > 3 or analyzerDelayMinutes > 3. |

**Weekly Score History — R7**

| Week | Date | L × C = Score | Notes / Changes |
|------|------|--------------|-----------------|
| W1–W9 | May 9 – Jul 7, 2026 | Not yet identified | — |
| W10 | Jul 13, 2026 | 4 × 2 = **8** | Added Week 10 after frontend integration testing. Polling delay noted during TC03 (Growing Backlog) simulation. |
| W11 | Jul 21, 2026 | 4 × 2 = **8** | Current state. Last-updated timestamp and health dot implemented. Risk accepted — alerting via Teams is unaffected by polling delay. |

---

### R8: Azure AD Easy Auth failure blocks or exposes production API

**Capability:** API Authentication  
**Current Status:** Active

| Field | Details |
|-------|---------|
| **Undesirable Event** | Azure AD Easy Auth failure blocks or exposes production API |
| **Risk Description** | Azure AD Easy Auth was added in v1.3.0 as the production API protection mechanism. If Easy Auth is misconfigured or fails, the entire API is either inaccessible to legitimate users (HTTP 401 on all requests) or exposed publicly without authentication. This risk was identified in Week 9 after the authentication feature was implemented and deployed. |
| **Initial Likelihood Score** | 2 / 5 (at identification) |
| **Likelihood Justification** | Easy Auth is an Azure-managed infrastructure feature that does not degrade due to application code changes. The primary risk is initial misconfiguration during setup or environment changes. Once correctly configured and verified, this risk is stable at Low likelihood. |
| **Initial Consequence Score** | 5 / 5 (at identification) |
| **Consequence Justification** | A misconfigured Easy Auth either denies all legitimate API access (total dashboard outage) or exposes all API endpoints publicly without authentication (security breach). Both outcomes are critical. The local development bypass (VITE_AUTH_ENABLED=false) ensures development is unaffected, but production impact is maximal. |
| **Initial Risk Score (L × C)** | 2 × 5 = 10 |
| **Mitigation** | Authentication is enforced at the Azure Functions infrastructure layer, not in application code, ensuring it cannot be bypassed by software bugs. Production configuration was verified by testing with a valid Bearer token (PASS — API responds normally) and an invalid/missing token (HTTP 401 confirmed). A local development bypass is provided via VITE_AUTH_ENABLED=false so test runs are unaffected by authentication requirements. The GET /api/health endpoint monitors function liveness and is the first indicator if auth misconfiguration causes broader API failure. PRD Reference: UE-7.1-01, FR-7.1.1. |
| **Current Likelihood / Consequence** | 2 / 5 × 4 / 5 (Week 11) |
| **Net Score (Post-Mitigation)** | 2 × 4 = 8 |
| **Net Score Justification** | Post-mitigation residual score (Week 11). Consequence reduced from 5 to 4 after Easy Auth was validated with valid and invalid tokens in the Azure production environment. Residual risk is Azure infrastructure-level failure beyond project control. PRD Reference: UE-7.1-01, FR-7.1.1. |

| Q1 — Desired Behavior | Q2 — Preventative Behavior | Q3 — Responsive Behavior |
|-----------------------|---------------------------|--------------------------|
| The backend API shall require Azure AD Easy Auth protection in all production deployments. The frontend shall acquire a Bearer token via MSAL on every API call and attach it as an Authorization header. | The production API shall not serve any response to requests that lack a valid Azure AD Bearer token. The frontend shall not make unauthenticated API calls when VITE_AUTH_ENABLED is set to true. | When Easy Auth rejects a request with HTTP 401, the frontend shall redirect the user to the MSAL login flow via loginRedirect. When MSAL token acquisition fails, the frontend shall not attempt acquireTokenPopup — it shall use acquireTokenRedirect instead to avoid browser popup blocking. |

| Functional Requirements (Q1) | Preventative Requirements (Q2) | Responsive Requirements (Q3) |
|------------------------------|-------------------------------|------------------------------|
| FR-7.1.1: The backend API shall require Azure AD Easy Auth protection in production environments to secure access to the Functions host. FR-7.2.1: The frontend shall provide a token acquisition flow that allows authenticated API requests through the dashboard. | FR-R8.2: The production API shall return HTTP 401 for all requests that do not carry a valid Azure AD Bearer token. FR-R8.3: The frontend shall not suppress authentication failures silently — all auth errors shall redirect the user to the login flow. | FR-R8.4: When MSAL token acquisition fails, the frontend shall use loginRedirect rather than loginPopup to avoid browser popup blocking errors. FR-R8.5: The GET /api/health endpoint shall report Degraded if the Functions host has not processed a successful request within 3 minutes. |

**Weekly Score History — R8**

| Week | Date | L × C = Score | Notes / Changes |
|------|------|--------------|-----------------|
| W1–W8 | May 9 – Jun 27, 2026 | Not yet identified | Risk not yet present — Azure AD Easy Auth not yet implemented (added v1.3.0, Jul 13). |
| W9 | Jul 7, 2026 | 2 × 5 = **10** | Identified after v1.3.0 auth implementation. Easy Auth deployed; production token validation verified. Initial score 10 (High). |
| W10 | Jul 13, 2026 | 2 × 4 = **8** | Consequence reduced to 4 after Easy Auth validated with valid and invalid tokens in production. Net score 8 (Medium). |
| W11 | Jul 21, 2026 | 2 × 4 = **8** | Current state. Risk accepted — Easy Auth correctly configured and verified in production. Residual risk is Azure infrastructure failure beyond project control. |

---

## Appendix A: Risk Report to PRD Undesirable Event Mapping

This appendix maps each risk tracked in this Risk Management Report to its corresponding Undesirable Event (UE) identifier in the QBIS Product Requirements Document (PRD), providing cross-document traceability. The PRD uses a formal UE ID scheme (UE-X.Y-ZZ) tied to Level-2 capabilities. This Risk Report uses an operational R-number scheme derived from project development experience. The two schemes are complementary: the PRD identifies product behavior risks tied to system capabilities, while this report additionally tracks project management risks that the PRD's UE table was not designed to capture.

| Risk Report ID | PRD UE ID | Risk Name | PRD UE Statement | Note |
|----------------|-----------|-----------|------------------|------|
| R1 | UE-1.2-01 | Azure Monitor ingestion delay | If Azure Monitor data is delayed or unavailable, the analyzer may misclassify rising or draining conditions because rate-based decisions are secondary to the Service Bus Admin API. | Direct match |
| R2 (RETIRED) | UE-2.3-01 | Division-by-zero in wait time | If wait time or SLA status is estimated incorrectly, a queue may be treated as healthy when it is actually breaching. | Resolved v1.3.3 |
| R3 | UE-3.2-01 | Teams webhook silent rejection | If Teams notifications fail, incident response may be delayed despite the underlying queue issue being valid. | Direct match |
| R4 | (none) | Dashboard scope creep | Not applicable — scope creep is a project management risk, not a product behavior risk. | Project risk only; no PRD UE |
| R5 | UE-3.2-01 | Teams URL pattern detection brittle | If Teams notifications fail, incident response may be delayed despite the underlying queue issue being valid. | Sub-risk of UE-3.2-01 |
| R6 | UE-2.3-01 | New queue UNKNOWN for 1–2 cycles | If wait time or SLA status is estimated incorrectly, a queue may be treated as healthy when it is actually breaching. | Sub-risk of UE-2.3-01 |
| R7 | UE-5.1-01 | Dashboard 30s polling delay | If queue summaries are stale or incomplete, the dashboard may mislead operators about the current multi-queue status. | Direct match |
| R8 | UE-7.1-01 | Azure AD Easy Auth failure | If API protection through Azure AD Easy Auth fails, request handling may be blocked or exposed incorrectly in production. | Added Week 9 |

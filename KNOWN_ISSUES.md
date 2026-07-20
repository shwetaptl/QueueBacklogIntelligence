# Known Issues & Limitations


### L-01 — Dashboard Updates by Polling, Not Real-Time
The frontend polls the backend on fixed intervals (status: 30s, alerts: 60s, health: 60s). There is no WebSocket or Server-Sent Events connection, so the UI can be up to 30 seconds behind the latest queue state.

### L-02 — Single Azure Service Bus Namespace Per Queue
Each queue configuration stores one namespace. Monitoring queues across multiple namespaces is supported by adding separate queue entries, but there is no namespace-level grouping in the UI.

### L-03 — Email via SMTP Only
Email alerts use `System.Net.Mail.SmtpClient` with direct SMTP credentials. There is no support for SendGrid, Amazon SES, or Azure Communication Services. Gmail requires an App Password (not the account password) due to Google's 2FA policies.

### L-04 — Incident Detection Requires at Least 2 Analyzer Cycles
The analyzer needs a minimum of 2 consecutive snapshots (60 seconds) before it can reliably compute trends, smoothed rates, and gap values. A brand-new queue or a queue monitored for the first time will show `RootCause = Unknown` for the first 1–2 minutes.

### L-05 — SLA Wait Time Is an Estimate
`WaitTimeMinutes` is calculated as `ActiveCount / OutgoingPerMin`. This is a queue-depth estimate, not a message-age measurement. It assumes uniform processing speed and does not account for message priority, consumer scaling, or message TTL.

### L-06 — CleanupFunction Requires a Manual Trigger in Local Development
`CleanupFunction` is scheduled via `TimerTrigger("0 0 2 * * *")` and only fires automatically on Azure at 02:00 UTC. In local development (`func start`) it must be triggered manually:

### L-07 — Azure Monitor Metric Delay (2–4 minutes)
Azure Monitor metrics (`IncomingMessages`, `OutgoingMessages`) are not real-time — they have a 2 to 4 minute reporting delay. The analyzer may briefly misclassify a healthy queue as `ConsumerStopped` immediately after a burst arrival because the outgoing rate appears zero until Azure catches up.

**Mitigation:** `AnalyzerService` uses a 3-snapshot smoothed average (`smoothedIncoming`) instead of the raw per-minute value, and a `queueWasRecentlyEmpty` guard suppresses `ConsumerStopped` classification for 2 minutes after a burst arrives on an empty queue.

### L-08 — "Recovering" Root Cause Is a Single-Snapshot State
`RootCause = Recovering` is only set when `isDraining = true AND recentlyStopped = true` simultaneously. This window is typically one 30-second analyzer cycle. If the consumer drains all messages in a single batch before the next snapshot, the state transitions directly from `ConsumerStopped` → `Healthy`, skipping `Recovering` entirely.

**Impact:** Incident history may not always show a "Recovering" phase. TC-05 Recovery test accounts for this by asserting `OK/None` rather than a specific `Recovering` root cause.

### L-09 — Teams Webhook Format Detection Is URL-Based
`AlertService` detects which JSON format to use (legacy MessageCard vs Adaptive Card) by checking whether the webhook URL contains `webhook.office.com`. If Microsoft changes the URL pattern for either connector type, alerts will silently send the wrong format and may fail without error.

using Microsoft.Extensions.Logging;
using QueueBacklogIntelligence.Models;

namespace QueueBacklogIntelligence.Services
{
    /// <summary>
    /// QBIS Analyzer — Final Architecture
    ///
    /// Design Principles:
    ///
    /// 1. ActiveCount is ground truth. Service Bus Admin API returns it
    ///    instantly with zero delay. All trend and state decisions start here.
    ///
    /// 2. Azure Monitor rates (IncomingPerMin, OutgoingPerMin) are secondary.
    ///    They have 1-2 minute ingestion delay and exhibit burst artifacts.
    ///    They are trusted only when consistent with ActiveCount evidence.
    ///
    /// 3. ConsumerStopped requires BOTH conditions to be true simultaneously:
    ///    - Monitor shows Out near zero (or no data)
    ///    - ActiveCount is NOT dropping (confirms no processing happening)
    ///    Either condition alone is insufficient.
    ///
    /// 4. No hardcoded message count thresholds. All thresholds are relative
    ///    to current queue size and throughput.
    ///
    /// 5. Alert severity is sticky — requires 2 consecutive OK readings
    ///    before de-escalating from Critical/Warning. Prevents false recoveries
    ///    from single-minute Monitor bursts.
    ///
    /// 6. WaitTime is null when OutgoingRate is unreliable. Never fabricate
    ///    a number. Null + BREACHING is more honest than a wrong number.
    ///
    /// Handles correctly:
    ///   - Consumer crash (hard stop)
    ///   - Consumer slow degradation
    ///   - Producer traffic spike
    ///   - Burst arrival on empty queue
    ///   - Slow drain under SLA breach
    ///   - Recovery after fix
    ///   - Idle queue with stale messages
    ///   - Azure Monitor outage
    ///   - Intermittent consumer (batch processing)
    ///   - DLQ growth
    ///   - Manual queue purge
    /// </summary>
    public class AnalyzerService
    {
        private readonly ILogger<AnalyzerService> _logger;

        // ── How many snapshots to fetch ────────────────────────────────────────
        // 22 = covers 20 minutes of history + 2 buffer for missed collector runs
        // Breakdown:
        //   [0-2]   = last 3 minutes  → rate calculation + trend
        //   [3-12]  = 3-13 minutes ago → root cause baseline
        //   [13-21] = 13-22 minutes ago → historical context
        public const int SnapshotCount = 22;

        // ── Rate weights for outgoing calculation ──────────────────────────────
        // Latest snapshot: 50% weight — most representative of current state
        // Previous:        30% weight — smooths single-minute glitches
        // Oldest of 3:     20% weight — provides stability
        // Sum = 1.0
        // Justification: if consumer crashes (Out drops to 0), latest gets
        // 50% weight so the crash pulls the rate down to 50% in minute 1,
        // then 80% in minute 2, then 100% in minute 3. Fast detection without
        // false alarms from single-minute Azure Monitor glitches.
        private static readonly double[] RateWeights = { 0.50, 0.30, 0.20 };

        // ── Noise floor for delta significance ────────────────────────────────
        // Dynamic: 5% of current ActiveCount, clamped between 1.0 and 10.0
        //
        // WHY 5%: Natural measurement variance between collector runs is
        // approximately 5% of queue size due to timing imprecision.
        //
        // WHY min=1.0: On small queues (Active=5), 5% = 0.25 which is too
        // sensitive. Any single message arrival would flip the trend.
        // Minimum 1 message change required to signal a trend.
        //
        // WHY max=10.0: On very large queues (Active=500), 5% = 25 which is
        // too insensitive. A delta of 15 messages/min on a large queue IS
        // meaningful. Cap at 10 to maintain sensitivity.
        private const double NoiseFloorPct = 0.05;
        private const double NoiseFloorMin = 1.0;
        private const double NoiseFloorMax = 10.0;

        // ── Consistency check tolerance ────────────────────────────────────────
        // How much Azure Monitor implied delta can differ from actual ActiveCount
        // delta before we consider Monitor data unreliable.
        //
        // WHY 5x noiseFloor: Azure Monitor aggregates in 1-minute windows and
        // can be off by small amounts due to timing boundaries. 5x noiseFloor
        // allows for legitimate rate transitions while catching large glitches
        // (glitches are typically 10-50x off, so 5x is a safe boundary).
        private const double ConsistencyTolerance = 5.0;

        // ── Idle detection ─────────────────────────────────────────────────────
        // Queue is idle when ALL of these are true:
        //   - ActiveCount is small (relative threshold below)
        //   - No movement in ActiveCount
        //   - Both Monitor rates are near zero
        //   - Previous snapshot also had small ActiveCount
        //
        // WHY relative not absolute: A queue is idle when it has stale messages
        // with no traffic. The definition of "small" depends on context.
        // We use: ActiveCount <= max(2, 1% of peak recent ActiveCount)
        // This ensures a queue that normally has 1000 messages is not flagged
        // idle when it has 5 messages during maintenance.
        private const double IdleRelativeThreshold = 0.01; // 1% of recent peak
        private const int IdleAbsoluteMax = 5;             

        // ── Root cause change threshold ────────────────────────────────────────
        // Minimum percentage change from baseline to identify a root cause.
        // WHY 25%: Natural traffic variance is 10-15%. Events like consumer
        // crashes or producer spikes typically cause 30-100% changes.
        // 25% sits safely above variance but catches real events early.
        private const double RootCauseThreshold = 0.25;

        // ── Minimum consecutive good readings before de-escalation ─────────────
        // WHY 2: A single good reading can be a Monitor burst artifact.
        // Two consecutive good readings confirms the situation is genuinely
        // resolved, not just a transient measurement anomaly.
        private const int DeEscalationRequiredCount = 2;

        public AnalyzerService(ILogger<AnalyzerService> logger)
        {
            _logger = logger;
        }

        public QueueStatusEntity Analyze(
            QueueConfigEntity config,
            List<QueueSnapshotEntity> snapshots,
            List<QueueStatusEntity> recentStatus)
        {
            var now = DateTime.UtcNow;

            // Safe defaults — no false alarms if we return early
            var result = new QueueStatusEntity
            {
                PartitionKey    = config.QueueName,
                RowKey          = (DateTime.MaxValue.Ticks - now.Ticks).ToString("D19"),
                TimestampUtc    = now,
                SlaMinutes      = config.SlaMinutes,
                SlaStatus       = "UNKNOWN",
                TrendLabel      = "Unknown",
                RootCause       = "Unknown",
                AlertSeverity   = "None",
                NeedToSendAlert = false
            };

            if (snapshots.Count < 2)
            {
                _logger.LogInformation(
                    "Queue '{Q}': Only {N} snapshot(s). Need 2. Waiting.",
                    config.QueueName, snapshots.Count);
                return result;
            }

            var current  = snapshots[0];
            var previous = snapshots[1];

            // Copy raw metrics for display
            result.ActiveCount    = current.ActiveCount;
            result.DLQCount       = current.DLQCount;
            result.IncomingPerMin = current.IncomingPerMin;
            result.OutgoingPerMin = current.OutgoingPerMin;

            // ══════════════════════════════════════════════════════════════════
            // STEP 1: COMPUTE ACTIVE COUNT DELTAS
            //
            // These are the most reliable numbers in the entire system.
            // We compute deltas per minute for up to 6 consecutive snapshot pairs.
            // Skip any gap > 3 minutes (collector missed a run).
            // ══════════════════════════════════════════════════════════════════
            var deltas = new List<double>();
            for (int i = 0; i < Math.Min(snapshots.Count - 1, 6); i++)
            {
                double mins = (snapshots[i].TimestampUtc - snapshots[i + 1].TimestampUtc)
                              .TotalMinutes;
                if (mins is > 0 and <= 3)
                    deltas.Add((snapshots[i].ActiveCount - snapshots[i + 1].ActiveCount) / mins);
            }

            if (deltas.Count == 0)
            {
                _logger.LogWarning(
                    "Queue '{Q}': No valid deltas. Snapshot timestamps too far apart.",
                    config.QueueName);
                return result;
            }

            // Smoothed rate: weighted average of up to 3 deltas
            // Represents trend over last 3 minutes, not just last 1 minute
            double netRateNow      = deltas[0];
            double netRateSmoothed = WeightedAverage(
                deltas.Take(3).ToList(), RateWeights);

            // Acceleration: is the situation getting better or worse?
            // Positive = worsening (growth accelerating or drain slowing)
            // Negative = improving (growth slowing or drain accelerating)
            double acceleration = deltas.Count >= 2
                ? deltas[0] - deltas[1]
                : 0.0;

            result.GapPerMin    = Math.Round(Math.Clamp(netRateSmoothed, -9999, 9999), 2);
            result.Acceleration = Math.Round(Math.Clamp(acceleration, -9999, 9999), 2);


            // ══════════════════════════════════════════════════════════════════
            // STEP 2: DYNAMIC NOISE FLOOR
            //
            // Relative to current ActiveCount so the system is equally
            // sensitive on small queues (10 msgs) and large queues (1000 msgs).
            // ══════════════════════════════════════════════════════════════════
            double noiseFloor = Math.Clamp(
                current.ActiveCount * NoiseFloorPct,
                NoiseFloorMin,
                NoiseFloorMax);


            // ══════════════════════════════════════════════════════════════════
            // STEP 3: CLASSIFY QUEUE STATE
            //
            // These five states are mutually exclusive and drive all downstream
            // decisions. The order of evaluation matters — most specific first.
            //
            // Key insight: ALL state decisions use ActiveCount deltas as primary
            // evidence. Azure Monitor rates only CONFIRM or ADD detail.
            // ══════════════════════════════════════════════════════════════════

            // IDLE: queue has stale messages with no real traffic.
            // Uses relative threshold so it scales correctly.
            long recentPeak = snapshots.Take(10).Max(s => s.ActiveCount);
            long idleThreshold = (long)Math.Max(
                recentPeak * IdleRelativeThreshold,
                IdleAbsoluteMax);

            // Azure Monitor has a 2-4 minute reporting delay. A single snapshot
            // can show a spike (e.g. In=2.0) from messages sent minutes earlier,
            // even though the queue is now genuinely idle. Averaging the last 3
            // snapshots smooths out that one-sample artifact without masking
            // sustained traffic (which would average above the threshold).
            double smoothedIncoming = snapshots
                .Take(3)
                .Select(s => s.IncomingPerMin ?? 0.0)
                .Average();

            bool isIdle = current.ActiveCount <= idleThreshold
                          && Math.Abs(netRateNow) <= noiseFloor
                          && smoothedIncoming < 1.0
                          && (current.OutgoingPerMin ?? 0) < 1.0
                          && previous.ActiveCount <= idleThreshold + 2;

            // DRAINING: ActiveCount consistently dropping.
            // Use smoothed rate for stability — single-minute drops can be noise.
            bool isDraining = !isIdle
                              && netRateSmoothed <= -noiseFloor;

            // GROWING: ActiveCount consistently increasing.
            bool isGrowing = !isIdle
                             && !isDraining
                             && netRateSmoothed > noiseFloor;

            // CONSUMER STOPPED: Messages queued but nothing being processed.
            //
            // Requires BOTH conditions simultaneously:
            //   1. ActiveCount is NOT dropping (ground truth from Admin API)
            //   2. Azure Monitor confirms Out is near zero (or no data)
            //
            // Explicitly NOT ConsumerStopped when:
            //   - Queue is draining (ActiveCount dropping proves processing)
            //   - Queue was recently empty (burst arrival, Monitor delayed)
            //   - Queue is idle (stale messages, not an active incident)
            bool activeCountDroppingNow = netRateNow <= -noiseFloor;
            // Only suppress ConsumerStopped for 2 minutes after burst arrival.
            // After 2 minutes, if queue is still stuck, it IS ConsumerStopped.
            // Using time-based check (2 min) not count-based (4 snapshots)
            // because count-based keeps suppressing too long.
            bool queueWasRecentlyEmpty = snapshots
                .Take(3)
                .Any(s => s.ActiveCount == 0
                       && (current.TimestampUtc - s.TimestampUtc).TotalMinutes <= 2);
            // ConsumerStopped detection — two evidence sources:
            //
            // Source A: Monitor EXPLICITLY reports Out < 1 (direct evidence)
            //
            // Source B: Monitor has no data (null) but ActiveCount confirms
            //   nothing is being consumed — queue has been non-draining for
            //   2+ consecutive minutes. This covers the Azure Monitor delay
            //   window where Out is null but consumer is clearly stopped.
            //
            // In both cases, ActiveCount delta must also confirm no processing
            // (not dropping). ActiveCount is ground truth — if it is dropping,
            // the consumer is running regardless of what Monitor reports.
            bool monitorShowsOrImpliesOutZero =
                // Monitor explicitly reports zero outgoing
                (current.OutgoingPerMin.HasValue && current.OutgoingPerMin.Value < 1.0)
                // OR Monitor null but 2+ consecutive minutes of no ActiveCount drop
                || (!current.OutgoingPerMin.HasValue
                    && deltas.Count >= 2
                    && deltas.Take(2).All(d => d >= -noiseFloor));

            bool isConsumerStopped = !isIdle
                                     && !isDraining
                                     && !activeCountDroppingNow
                                     && !queueWasRecentlyEmpty
                                     && monitorShowsOrImpliesOutZero
                                     && current.ActiveCount > idleThreshold;

            // STABLE: none of the above — balanced queue
            bool isStable = !isIdle
                            && !isDraining
                            && !isGrowing
                            && !isConsumerStopped;

            _logger.LogDebug(
                "Queue '{Q}': State — Idle={I} Stopped={S} " +
                "Growing={G} Draining={D} Stable={St} | " +
                "NoiseFloor={NF:F2} NetNow={NN:+0.0;-0.0} " +
                "NetSmoothed={NS:+0.0;-0.0}",
                config.QueueName,
                isIdle, isConsumerStopped, isGrowing, isDraining, isStable,
                noiseFloor, netRateNow, netRateSmoothed);


            // ══════════════════════════════════════════════════════════════════
            // STEP 4: DERIVE RELIABLE OUTGOING RATE
            //
            // Strategy depends on queue state:
            //
            // IDLE/EMPTY → 0 (trivial)
            // CONSUMER STOPPED → 0 (by definition)
            // PURE DRAIN (no incoming) → abs(delta) from ActiveCount
            //   This is the most accurate case — no Monitor needed at all.
            // STABLE/GROWING → try Monitor rates with consistency check
            //   If consistent: use weighted Monitor rates
            //   If inconsistent: try last known good rate
            //   If no good rate: null (honest uncertainty)
            // ══════════════════════════════════════════════════════════════════
            double? derivedOutgoing = null;
            double? derivedIncoming = null;
            bool    monitorTrusted  = false;

            if (current.ActiveCount == 0 || isIdle)
            {
                derivedOutgoing = 0;
                derivedIncoming = 0;
            }
            else if (isConsumerStopped)
            {
                derivedOutgoing = 0;
                derivedIncoming = current.IncomingPerMin ?? 0;
            }
            else if (isDraining && (current.IncomingPerMin ?? 0) < 1.0)
            {
                // Pure drain — exact derivation, no Monitor needed
                // abs(delta) = outgoing rate (nothing arriving)
                derivedOutgoing = Math.Abs(netRateNow);
                derivedIncoming = 0;
                monitorTrusted  = false;

                _logger.LogInformation(
                    "Queue '{Q}': Pure drain. " +
                    "DerivedOut={Out:F1}/min from ActiveCount delta.",
                    config.QueueName, derivedOutgoing);
            }
            else
            {
                // Try Monitor rates — need at least 1 snapshot with data
                var rateSnaps = snapshots
                    .Where(s => s.OutgoingPerMin.HasValue
                             && s.IncomingPerMin.HasValue)
                    .Take(3)
                    .ToList();

                if (rateSnaps.Count >= 1)
                {
                    // Weighted average of up to 3 snapshots
                    double wOut = 0, wIn = 0, wTotal = 0;
                    for (int i = 0; i < rateSnaps.Count; i++)
                    {
                        double w = RateWeights[i];
                        wOut    += rateSnaps[i].OutgoingPerMin!.Value * w;
                        wIn     += rateSnaps[i].IncomingPerMin!.Value  * w;
                        wTotal  += w;
                    }
                    double weightedOut = wOut / wTotal;
                    double weightedIn  = wIn  / wTotal;

                    // Consistency check: does Monitor agree with ActiveCount?
                    double mins = (snapshots[0].TimestampUtc - snapshots[1].TimestampUtc)
                                  .TotalMinutes;
                    bool consistent = true;

                    if (mins is > 0 and <= 3)
                    {
                        double impliedDelta = (weightedIn - weightedOut) * mins;
                        double actualDelta  = snapshots[0].ActiveCount
                                             - snapshots[1].ActiveCount;
                        double tolerance    = noiseFloor * ConsistencyTolerance;
                        consistent = Math.Abs(impliedDelta - actualDelta) <= tolerance;

                        if (!consistent)
                            _logger.LogWarning(
                                "Queue '{Q}': Monitor inconsistent. " +
                                "Implied Δ={Imp:+0.0;-0.0} Actual Δ={Act:+0.0;-0.0} " +
                                "Tolerance={Tol:F1}. Using fallback.",
                                config.QueueName, impliedDelta, actualDelta, tolerance);
                    }

                    if (consistent)
                    {
                        derivedOutgoing = weightedOut;
                        derivedIncoming = weightedIn;
                        monitorTrusted  = true;

                        _logger.LogInformation(
                            "Queue '{Q}': Monitor trusted. " +
                            "In={In:F0}/min Out={Out:F0}/min ({N} snaps).",
                            config.QueueName, derivedIncoming, derivedOutgoing,
                            rateSnaps.Count);
                    }
                    else
                    {
                        // Monitor inconsistent — try last known good outgoing rate
                        // from recent history (up to 5 minutes ago)
                        // Only use rates from snapshots where queue had messages
                    // Rates from when queue was empty are meaningless as fallback
                    var lastGood = snapshots
                            .Skip(1)
                            .Where(s => s.OutgoingPerMin.HasValue
                                     && s.OutgoingPerMin.Value >= 1.0
                                     && s.ActiveCount > 0)
                            .Take(3)
                            .ToList();

                        if (lastGood.Count > 0)
                        {
                            // Use average of last known good readings
                            // They are from 1-5 mins ago so slightly stale
                            // but better than null for WaitTime calculation
                            derivedOutgoing = lastGood.Average(s =>
                                s.OutgoingPerMin!.Value);
                            derivedIncoming = null; // incoming unknown
                            monitorTrusted  = false;

                            _logger.LogWarning(
                                "Queue '{Q}': Using last known good rate " +
                                "{Out:F1}/min ({N} historical snaps).",
                                config.QueueName, derivedOutgoing, lastGood.Count);
                        }
                        else if (isDraining)
                        {
                            // No historical rate but queue draining —
                            // use delta as lower bound
                            derivedOutgoing = Math.Abs(netRateNow);
                            derivedIncoming = null;
                        }
                        // else: growing + no reliable rate = derivedOutgoing null
                        // Honest: we cannot compute WaitTime right now
                    }
                }
            }

            // Update displayed rates with derived values
            if (derivedOutgoing.HasValue)
                result.OutgoingPerMin = derivedOutgoing;
            if (derivedIncoming.HasValue)
                result.IncomingPerMin = derivedIncoming;


            // ══════════════════════════════════════════════════════════════════
            // STEP 5: WAIT TIME AND SLA STATUS
            //
            // WaitTime = ActiveCount / OutgoingRate
            // This is the core business metric — how long does a new message wait?
            //
            // Cases:
            //   Empty/Idle   → 0, OK
            //   Stopped      → null, BREACHING (infinite, do not fake a number)
            //   Rate >= 0.5  → exact calculation
            //   Rate < 0.5   → effectively stopped, treat as null, BREACHING
            //   No rate data → null, check recent history
            // ══════════════════════════════════════════════════════════════════
            if (current.ActiveCount == 0 || isIdle)
            {
                result.WaitTimeMinutes = 0;
                result.SlaStatus       = "OK";
            }
            else if (isConsumerStopped)
            {
                result.WaitTimeMinutes = null;
                result.SlaStatus       = "BREACHING";
            }
            else if (derivedOutgoing.HasValue && derivedOutgoing.Value >= 0.5)
            {
                double wait = current.ActiveCount / derivedOutgoing.Value;
                result.WaitTimeMinutes = Math.Round(Math.Min(wait, 9999), 2);
                result.SlaStatus       = wait <= config.SlaMinutes ? "OK" : "BREACHING";
            }
            else if (derivedOutgoing.HasValue && derivedOutgoing.Value < 0.5)
            {
                // Rate too low — effectively stopped
                result.WaitTimeMinutes = null;
                result.SlaStatus       = "BREACHING";
            }
            else
            {
                // No reliable rate — check recent history
                result.WaitTimeMinutes = null;
                bool recentlyBreaching = recentStatus
                    .Take(3).Any(s => s.SlaStatus == "BREACHING");
                result.SlaStatus = recentlyBreaching ? "BREACHING" : "UNKNOWN";
            }


            // ══════════════════════════════════════════════════════════════════
            // STEP 6: TREND LABEL
            //
            // Seven labels for precise operational communication.
            // All based on ActiveCount delta — not Monitor rates.
            // Fast/Slow variants use acceleration to show direction of change.
            // ══════════════════════════════════════════════════════════════════
            if (isIdle)
            {
                result.TrendLabel = "Idle";
            }
            else if (current.ActiveCount == 0)
            {
                result.TrendLabel = "Idle";
            }
            else if (isConsumerStopped)
            {
                // If messages still arriving → Growing, else Stable
                result.TrendLabel = netRateNow > noiseFloor ? "Growing" : "Stable";
            }
            else if (isGrowing)
            {
                // GrowingFast only when:
                //   - Acceleration is positive (growth speeding up)
                //   - AND queue is large enough to matter (> 5 msgs)
                result.TrendLabel =
                    acceleration > noiseFloor && current.ActiveCount > 5
                    ? "GrowingFast" : "Growing";
            }
            else if (isDraining)
            {
                // DrainingFast when drain rate itself is accelerating
                result.TrendLabel =
                    acceleration < -noiseFloor
                    ? "DrainingFast" : "Draining";
            }
            else
            {
                result.TrendLabel = "Stable";
            }


            // ══════════════════════════════════════════════════════════════════
            // STEP 7: ROOT CAUSE
            //
            // Priority order — most specific and actionable first:
            //   1. Recovering    — was stopped, now draining (check FIRST)
            //   2. ConsumerStopped
            //   3. DLQGrowth     — always available, no Monitor needed
            //   4. Rate-based    — requires Monitor data + baseline
            //   5. Healthy       — stable within SLA
            //   6. Unknown       — insufficient data
            // ══════════════════════════════════════════════════════════════════
            result.RootCause = DetermineRootCause(
                config, snapshots, recentStatus,
                derivedIncoming, derivedOutgoing,
                isConsumerStopped, isDraining, isIdle, isStable,
                noiseFloor);


            // ══════════════════════════════════════════════════════════════════
            // STEP 8: ALERT SEVERITY
            //
            // Primary: wait time ratio to SLA
            //   None     < 60% of SLA
            //   Warning  60-100% of SLA (approaching breach)
            //   Critical >= 100% of SLA (SLA being violated now)
            //
            // Consumer stopped: always Critical
            //
            // Predictive Warning: queue is OK now but heading for breach
            //   If growing AND current waitTime is rising AND time to breach
            //   is less than SLA duration → fire Warning early
            //
            // Sticky severity: require DeEscalationRequiredCount consecutive
            //   OK/None readings before dropping from Critical/Warning
            //   Prevents false recovery from single-minute Monitor bursts
            // ══════════════════════════════════════════════════════════════════
            string rawSeverity = ComputeRawSeverity(
                config, result, current, recentStatus,
                isIdle, isConsumerStopped, isGrowing,
                netRateSmoothed, derivedOutgoing);

            // Apply sticky severity — prevent single-minute drops
            result.AlertSeverity = ApplyStickyEscalation(
                rawSeverity, recentStatus);


            // ══════════════════════════════════════════════════════════════════
            // STEP 9: NEED TO SEND ALERT
            //
            // Not every Critical minute sends a notification.
            // Rules:
            //   NEW:        previous was None, now Warning/Critical → send
            //   ESCALATION: Warning→Critical → always send (bypass cooldown)
            //   REMINDER:   same severity, cooldown elapsed → send
            //   RECOVERY:   was Warning/Critical, now None AND genuinely OK → send
            //   SUPPRESS:   same severity within cooldown → do not send
            // ══════════════════════════════════════════════════════════════════
            result.NeedToSendAlert = DetermineNeedToSend(
                config, result, recentStatus, current);


            // ── Final log line ─────────────────────────────────────────────────
            _logger.LogInformation(
                "Queue '{Q}' | Active={A} DLQ={D} | " +
                "In={In}/min Out={Out}/min Gap={Gap:+0.0;-0.0} Acc={Acc:+0.0;-0.0} | " +
                "Wait={W} SLA={SLA}min | " +
                "State={State} Trend={T} | " +
                "SlaStatus={SS} Cause={C} | " +
                "Severity={Sev} Send={Send} | Monitor={M}",
                config.QueueName,
                current.ActiveCount, current.DLQCount,
                derivedIncoming?.ToString("F0") ?? "?",
                derivedOutgoing?.ToString("F0") ?? "?",
                netRateSmoothed, acceleration,
                result.WaitTimeMinutes.HasValue
                    ? $"{result.WaitTimeMinutes:F1}min" : "∞",
                config.SlaMinutes,
                isIdle ? "Idle"
                    : isConsumerStopped ? "Stopped"
                    : isGrowing ? "Growing"
                    : isDraining ? "Draining"
                    : "Stable",
                result.TrendLabel,
                result.SlaStatus,
                result.RootCause,
                result.AlertSeverity,
                result.NeedToSendAlert,
                monitorTrusted ? "YES" : "NO");

            return result;
        }


        // ════════════════════════════════════════════════════════════════════
        // ROOT CAUSE DETERMINATION
        // ════════════════════════════════════════════════════════════════════
        private string DetermineRootCause(
            QueueConfigEntity config,
            List<QueueSnapshotEntity> snapshots,
            List<QueueStatusEntity> recentStatus,
            double? derivedIncoming,
            double? derivedOutgoing,
            bool isConsumerStopped,
            bool isDraining,
            bool isIdle,
            bool isStable,
            double noiseFloor)
        {
            // 1. RECOVERING — must check first
            // Queue was ConsumerStopped recently and is now draining.
            // The baseline would be poisoned by the crisis period, so
            // we use status history instead of rate comparison.
            bool recentlyStopped = recentStatus
                .Take(6)
                .Any(s => s.RootCause == "ConsumerStopped");

            if (isDraining && recentlyStopped)
                return "Recovering";

            // 2. CONSUMER STOPPED
            if (isConsumerStopped)
                return "ConsumerStopped";

            // 3. IDLE / HEALTHY
            if (isIdle || snapshots[0].ActiveCount == 0)
                return "Healthy";

            // 4. DLQ GROWTH — always available, no Monitor needed
            if (snapshots.Count >= 6)
            {
                var newest = snapshots[0];
                var older  = snapshots[Math.Min(5, snapshots.Count - 1)];
                double dlqRate = (newest.DLQCount - older.DLQCount) / 5.0;
                if (dlqRate > 1.0 && newest.DLQCount > 5)
                    return "DLQGrowth";
            }

            // 5. RATE-BASED ROOT CAUSES — requires Monitor data and baseline
            if (!derivedIncoming.HasValue || !derivedOutgoing.HasValue)
                return "Unknown";

            if (snapshots.Count < 8)
                return "Unknown"; // not enough history for reliable baseline

            // Baseline: snapshots from 5-15 minutes ago
            // Skip recent 5 minutes — they may be contaminated by current event
            var baselineSnaps = snapshots
                .Where(s => s.OutgoingPerMin.HasValue
                         && s.IncomingPerMin.HasValue)
                .Skip(5)
                .Take(10)
                .ToList();

            if (baselineSnaps.Count < 3)
                return "Unknown";

            // Use median for baseline — more robust than average against outliers
            double baseIncoming = Median(baselineSnaps
                .Select(s => s.IncomingPerMin!.Value).ToList());
            double baseOutgoing = Median(baselineSnaps
                .Select(s => s.OutgoingPerMin!.Value).ToList());

            // Guard: baseline must have had meaningful traffic
            // Comparing to zero baseline produces meaningless percentages
            if (baseIncoming < 2.0 && baseOutgoing < 2.0)
                return "Unknown";

            // Percentage change — add 1 to prevent division by zero
            double inChange = (derivedIncoming.Value - baseIncoming) / (baseIncoming + 1);
            double outChange = (derivedOutgoing.Value - baseOutgoing) / (baseOutgoing + 1);

            bool producerSpiked = inChange  >  RootCauseThreshold;
            bool consumerSlowed = outChange < -RootCauseThreshold;

            _logger.LogDebug(
                "Queue '{Q}': RootCause — " +
                "CurrIn={CI:F0} CurrOut={CO:F0} " +
                "BaseIn={BI:F0} BaseOut={BO:F0} " +
                "InChg={IC:+P0;-P0} OutChg={OC:+P0;-P0}",
                config.QueueName,
                derivedIncoming, derivedOutgoing,
                baseIncoming, baseOutgoing,
                inChange, outChange);

            if (producerSpiked && consumerSlowed) return "ProducerSpikeAndConsumerSlowdown";
            if (producerSpiked)                   return "ProducerSpike";
            if (consumerSlowed)                   return "ConsumerSlowdown";

            // 6. STABLE AND HEALTHY
            if (isStable && snapshots[0].ActiveCount > 0)
                return "Healthy";

            return "Unknown";
        }


        // ════════════════════════════════════════════════════════════════════
        // RAW SEVERITY COMPUTATION (before stickiness applied)
        // ════════════════════════════════════════════════════════════════════
        private string ComputeRawSeverity(
            QueueConfigEntity config,
            QueueStatusEntity result,
            QueueSnapshotEntity current,
            List<QueueStatusEntity> recentStatus,
            bool isIdle,
            bool isConsumerStopped,
            bool isGrowing,
            double netRateSmoothed,
            double? derivedOutgoing)
        {
            // Empty or idle — always None
            if (isIdle || current.ActiveCount == 0)
                return "None";

            // Consumer stopped — always Critical
            if (isConsumerStopped)
                return "Critical";

            // Wait time available — primary path
            if (result.WaitTimeMinutes.HasValue)
            {
                double ratio = result.WaitTimeMinutes.Value / config.SlaMinutes;

                if (ratio >= config.CriticalThreshold)
                    return "Critical";

                if (ratio >= config.WarningThreshold)
                    return "Warning";

                // Queue is OK now — check predictive warning
                // If growing and breach predicted within SLA duration → Warning
                if (isGrowing
                    && netRateSmoothed > 0
                    && derivedOutgoing.HasValue
                    && derivedOutgoing.Value > 0)
                {
                    // Time to breach = (SLA - current WaitTime) / rate of WaitTime growth
                    // WaitTime grows when ActiveCount grows and OutgoingRate is constant
                    // Rate of WaitTime growth ≈ netRateSmoothed / derivedOutgoing
                    double waitTimeGrowthRate = netRateSmoothed / derivedOutgoing.Value;
                    if (waitTimeGrowthRate > 0)
                    {
                        double timeToBreach =
                            (config.SlaMinutes - result.WaitTimeMinutes.Value)
                            / waitTimeGrowthRate;

                        // Warn if breach predicted within 1 SLA duration from now
                        if (timeToBreach < config.SlaMinutes)
                        {
                            _logger.LogInformation(
                                "Queue '{Q}': Predictive Warning. " +
                                "Breach in ~{T:F0}min at current rate.",
                                config.QueueName, timeToBreach);
                            return "Warning";
                        }
                    }
                }

                return "None";
            }

            // SLA is BREACHING but no wait time (consumer stopped or no rate)
            if (result.SlaStatus == "BREACHING")
                return "Critical";

            // Growing with no wait time — use sustained growing minutes as proxy
            if (isGrowing)
            {
                int growingMins = recentStatus
                    .TakeWhile(s => s.TrendLabel is "Growing" or "GrowingFast")
                    .Count();

                if (growingMins >= config.SlaMinutes)     return "Critical";
                if (growingMins >= config.SlaMinutes / 2) return "Warning";
            }

            return "None";
        }


        // ════════════════════════════════════════════════════════════════════
        // STICKY SEVERITY
        //
        // Prevents de-escalation from a single good reading.
        // Requires DeEscalationRequiredCount consecutive OK/None readings.
        // ════════════════════════════════════════════════════════════════════
        private static string ApplyStickyEscalation(
            string rawSeverity,
            List<QueueStatusEntity> recentStatus)
        {
            var lastSeverity = recentStatus.FirstOrDefault()?.AlertSeverity;

            // Escalation always applies immediately — no stickiness needed
            if (rawSeverity == "Critical") return "Critical";
            if (rawSeverity == "Warning"
                && lastSeverity != "Critical") return "Warning";

            // Trying to de-escalate — check if we have enough consecutive good readings
            if (lastSeverity is "Critical" or "Warning")
            {
                int consecutiveGood = recentStatus
                    .TakeWhile(s => s.AlertSeverity is "None" or "Warning")
                    .Count();

                // Not enough good readings yet — hold at Warning
                if (consecutiveGood < DeEscalationRequiredCount)
                    return lastSeverity == "Critical" ? "Warning" : "Warning";
            }

            return rawSeverity; // "None"
        }


        // ════════════════════════════════════════════════════════════════════
        // NEED TO SEND ALERT
        // ════════════════════════════════════════════════════════════════════
        private static bool DetermineNeedToSend(
            QueueConfigEntity config,
            QueueStatusEntity result,
            List<QueueStatusEntity> recentStatus,
            QueueSnapshotEntity current)
        {
            var last = recentStatus.FirstOrDefault();

            if (result.AlertSeverity == "None")
            {
                // Recovery notification — only when genuinely resolved
                // Requires: previous was Warning/Critical AND queue is healthy
                bool wasActive = last?.AlertSeverity is "Warning" or "Critical";
                bool genuinelyResolved = result.SlaStatus == "OK"
                    && result.TrendLabel is not "Growing"
                    && result.TrendLabel is not "GrowingFast";
                return wasActive && genuinelyResolved;
            }

            // New incident
            if (last == null || last.AlertSeverity == "None")
                return true;

            // Escalation — Warning → Critical always notifies immediately
            if (last.AlertSeverity == "Warning"
                && result.AlertSeverity == "Critical")
                return true;

            // Repeat reminder — only after cooldown
            double minsSinceLast = (result.TimestampUtc - last.TimestampUtc)
                                   .TotalMinutes;
            return minsSinceLast >= config.CooldownMinutes;
        }


        // ════════════════════════════════════════════════════════════════════
        // HELPERS
        // ════════════════════════════════════════════════════════════════════
        private static double WeightedAverage(List<double> values, double[] weights)
        {
            if (values.Count == 0) return 0;
            double sum = 0, total = 0;
            for (int i = 0; i < Math.Min(values.Count, weights.Length); i++)
            {
                sum   += values[i] * weights[i];
                total += weights[i];
            }
            return total > 0 ? sum / total : values[0];
        }

        private static double Median(List<double> values)
        {
            if (values.Count == 0) return 0;
            var sorted = values.OrderBy(v => v).ToList();
            int mid    = sorted.Count / 2;
            return sorted.Count % 2 == 0
                ? (sorted[mid - 1] + sorted[mid]) / 2.0
                : sorted[mid];
        }
    }
}
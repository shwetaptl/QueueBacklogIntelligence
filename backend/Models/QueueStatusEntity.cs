using Azure;
using Azure.Data.Tables;

namespace QueueBacklogIntelligence.Models
{
    public class QueueStatusEntity : ITableEntity
    {
        // ── Table Storage keys ─────────────────────────────────────────────────
        // PartitionKey = queue name
        // RowKey       = reverse timestamp (DateTime.MaxValue.Ticks - now.Ticks)
        //                ensures newest rows sort first — efficient range queries
        public string  PartitionKey { get; set; } = string.Empty;
        public string  RowKey       { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag    ETag         { get; set; } = ETag.All;

        // ── When this status was computed ──────────────────────────────────────
        public DateTime TimestampUtc { get; set; }

        // ── Raw metrics (copied from QueueSnapshot for display) ───────────────
        public long    ActiveCount    { get; set; }
        public long    DLQCount       { get; set; }
        public double? IncomingPerMin { get; set; }   // from Azure Monitor (may be delayed)
        public double? OutgoingPerMin { get; set; }   // derived by analyzer (weighted/fallback)

        // ── Computed intelligence ─────────────────────────────────────────────

        /// <summary>
        /// How many minutes a message arriving RIGHT NOW will wait before
        /// being processed. null = infinite (consumer stopped) or unknown.
        /// </summary>
        public double? WaitTimeMinutes { get; set; }

        /// <summary>
        /// The SLA target in minutes from QueueConfig. Stored here so
        /// dashboards can compute SLA ratio without a second table lookup.
        /// </summary>
        public int SlaMinutes { get; set; }

        /// <summary>
        /// OK         — wait time within SLA
        /// BREACHING  — wait time exceeds SLA or consumer stopped
        /// UNKNOWN    — insufficient data to determine
        /// </summary>
        public string SlaStatus { get; set; } = "UNKNOWN";

        /// <summary>
        /// Net backlog change per minute.
        /// Positive = growing (bad), Negative = draining (good), 0 = balanced.
        /// Derived from smoothed ActiveCount delta — not Azure Monitor rates.
        /// </summary>
        public double? GapPerMin { get; set; }

        /// <summary>
        /// Is the rate of change itself speeding up or slowing?
        /// Positive = situation worsening (growth accelerating).
        /// Negative = situation improving (drain accelerating).
        /// </summary>
        public double? Acceleration { get; set; }

        /// <summary>
        /// Direction and intensity of backlog change:
        ///   Idle        — tiny stale backlog, no traffic
        ///   Stable      — balanced, within noise floor
        ///   Growing     — backlog accumulating
        ///   GrowingFast — growing AND growth rate itself accelerating
        ///   Draining    — being processed faster than arriving
        ///   DrainingFast — draining AND drain rate itself accelerating
        ///   Unknown     — insufficient data
        /// </summary>
        public string TrendLabel { get; set; } = "Unknown";

        /// <summary>
        /// Probable reason for current queue state:
        ///   Healthy                         — operating normally
        ///   ConsumerStopped                 — outgoing rate near zero
        ///   ConsumerSlowdown                — outgoing rate dropped 30%+ from baseline
        ///   ProducerSpike                   — incoming rate jumped 30%+ from baseline
        ///   ProducerSpikeAndConsumerSlowdown — both simultaneously
        ///   DLQGrowth                       — dead letter queue growing
        ///   Recovering                      — was ConsumerStopped, now draining
        ///   Unknown                         — pattern not identified
        /// </summary>
        public string RootCause { get; set; } = "Unknown";

        /// <summary>
        /// None     — queue healthy, no action needed
        /// Warning  — wait time >= 60% of SLA, or breach predicted soon
        /// Critical — SLA breaching now, or consumer stopped
        /// </summary>
        public string AlertSeverity { get; set; } = "None";

        /// <summary>
        /// TRUE when a notification should be sent this minute.
        /// Smart dedup: true only for new incidents, escalations,
        /// cooldown-elapsed reminders, and recovery events.
        /// FALSE for repeated same-severity within cooldown window.
        /// </summary>
        public bool NeedToSendAlert { get; set; }
    }
}
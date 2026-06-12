using Azure;
using Azure.Data.Tables;

namespace QueueBacklogIntelligence.Models
{
    // Raw data collected by Collector every minute.
    // Only real observable values — no derived fields.
    public class QueueSnapshotEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public DateTime TimestampUtc   { get; set; }
        public long     ActiveCount    { get; set; }
        public long     DLQCount       { get; set; }

        // From Azure Monitor — null if Monitor unavailable this minute
        public double? IncomingPerMin  { get; set; }
        public double? OutgoingPerMin  { get; set; }
    }
}
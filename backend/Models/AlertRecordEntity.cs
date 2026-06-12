using Azure;
using Azure.Data.Tables;

namespace QueueBacklogIntelligence.Models
{
    // One row per incident — not per alert message.
    // PartitionKey = queue name, RowKey = incident GUID.
    public class AlertRecordEntity : ITableEntity
    {
        public string          PartitionKey    { get; set; } = string.Empty;
        public string          RowKey          { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp       { get; set; }
        public ETag            ETag            { get; set; }

        public string    IncidentId       { get; set; } = string.Empty;
        public DateTime  OpenedAtUtc      { get; set; }
        public DateTime? ResolvedAtUtc    { get; set; }
        public string    Status           { get; set; } = "Open";   // "Open" | "Resolved"
        public string    PeakSeverity     { get; set; } = string.Empty;
        public string    FirstRootCause   { get; set; } = string.Empty;
        public DateTime  LastAlertSentUtc { get; set; }
        public int       AlertCount       { get; set; }
    }
}

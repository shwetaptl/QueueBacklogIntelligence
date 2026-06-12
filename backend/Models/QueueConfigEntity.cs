using Azure;
using Azure.Data.Tables;

namespace QueueBacklogIntelligence.Models
{
    public class QueueConfigEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = "config";
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public string QueueName         { get; set; } = string.Empty;
        public string Namespace         { get; set; } = string.Empty;
        public string SubscriptionId    { get; set; } = string.Empty;
        public string ResourceGroupName { get; set; } = string.Empty;
        public int    SlaMinutes        { get; set; } = 15;
        public bool   IsEnabled         { get; set; } = true;
        public int    CooldownMinutes   { get; set; } = 10;
        public double WarningThreshold  { get; set; } = 0.75;
        public double CriticalThreshold { get; set; } = 1.0;
        public string? TeamsWebhookUrl  { get; set; }
        public string? EmailRecipients  { get; set; }
    }
}
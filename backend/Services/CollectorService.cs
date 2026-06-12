using Azure.Messaging.ServiceBus.Administration;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using Microsoft.Extensions.Logging;
using QueueBacklogIntelligence.Models;

namespace QueueBacklogIntelligence.Services
{
    public class CollectorService
    {
        private readonly ServiceBusAdministrationClient _sbClient;
        private readonly MetricsQueryClient             _monitorClient;
        private readonly ILogger<CollectorService>      _logger;

        public CollectorService(
            ServiceBusAdministrationClient sbClient,
            MetricsQueryClient monitorClient,
            ILogger<CollectorService> logger)
        {
            _sbClient      = sbClient;
            _monitorClient = monitorClient;
            _logger        = logger;
        }

        public async Task<QueueSnapshotEntity> CollectAsync(
            QueueConfigEntity config,
            DateTime runTime,
            CancellationToken ct = default)
        {
            var rowKey = (DateTime.MaxValue.Ticks - runTime.Ticks)
                .ToString("D19");

            // ── Fetch from Service Bus Admin API ──────────────────────────
            // ActiveCount and DLQCount
            var props = (await _sbClient
                .GetQueueRuntimePropertiesAsync(config.QueueName, ct)).Value;

            // ── Fetch from Azure Monitor ──────────────────────────────────
            // IncomingMessages and OutgoingMessages 
            double? incomingPerMin = null;
            double? outgoingPerMin = null;

            try
            {
                var resourceId =
                    $"/subscriptions/{config.SubscriptionId}" +
                    $"/resourceGroups/{config.ResourceGroupName}" +
                    $"/providers/Microsoft.ServiceBus" +
                    $"/namespaces/{config.Namespace}";

                var metrics = await _monitorClient.QueryResourceAsync(
                    resourceId,
                    new[] { "IncomingMessages", "OutgoingMessages" },
                    new MetricsQueryOptions
                    {
                        TimeRange   = new QueryTimeRange(TimeSpan.FromMinutes(3)),
                        Granularity = TimeSpan.FromMinutes(1),
                        Filter      = $"EntityName eq '{config.QueueName}'"
                    },
                    ct);

                foreach (var metric in metrics.Value.Metrics)
                {
                    var point = metric.TimeSeries
                        .SelectMany(ts => ts.Values)
                        .Where(v => v.Total.HasValue)
                        .OrderByDescending(v => v.TimeStamp)
                        .FirstOrDefault();

                    if (point?.Total == null) continue;

                    if (metric.Name == "IncomingMessages")
                        incomingPerMin = point.Total.Value;
                    else if (metric.Name == "OutgoingMessages")
                        outgoingPerMin = point.Total.Value;
                }
            }
            catch (Exception ex)
            {
                // Azure Monitor unavailable — rates stay null
                // Analyzer will handle null rates gracefully
                _logger.LogWarning(
                    "Azure Monitor unavailable for '{Queue}': {Error}",
                    config.QueueName, ex.Message);
            }

            _logger.LogInformation(
                "Collected '{Queue}': Active={Active} DLQ={DLQ} " +
                "In={In}/min Out={Out}/min",
                config.QueueName,
                props.ActiveMessageCount,
                props.DeadLetterMessageCount,
                incomingPerMin?.ToString("F0") ?? "N/A",
                outgoingPerMin?.ToString("F0") ?? "N/A");

            return new QueueSnapshotEntity
            {
                PartitionKey   = config.QueueName,
                RowKey         = rowKey,
                TimestampUtc   = runTime,
                ActiveCount    = props.ActiveMessageCount,
                DLQCount       = props.DeadLetterMessageCount,
                IncomingPerMin = incomingPerMin,
                OutgoingPerMin = outgoingPerMin
            };
        }
    }
}
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using QueueBacklogIntelligence.Models;

namespace QueueBacklogIntelligence.Services
{
    public class Repository : IRepository
    {
        private readonly TableServiceClient _client;
        private readonly ILogger<Repository> _logger;

        private const string ConfigTable   = "QueueConfig";
        private const string SnapshotTable = "QueueSnapshot";
        private const string StatusTable   = "QueueStatus";
        private const string AlertTable    = "AlertRecord";

        public Repository(string connectionString, ILogger<Repository> logger)
        {
            _client = new TableServiceClient(connectionString);
            _logger = logger;
        }

        public async Task EnsureTablesExistAsync(CancellationToken ct = default)
        {
            foreach (var name in new[]
                { ConfigTable, SnapshotTable, StatusTable, AlertTable })
            {
                await _client.GetTableClient(name).CreateIfNotExistsAsync(ct);
            }
            _logger.LogInformation("All tables verified.");
        }

        public async Task<List<QueueConfigEntity>> GetEnabledQueuesAsync(
            CancellationToken ct = default)
        {
            var table   = _client.GetTableClient(ConfigTable);
            var results = new List<QueueConfigEntity>();
            await foreach (var e in table.QueryAsync<QueueConfigEntity>(
                filter: "PartitionKey eq 'config' and IsEnabled eq true",
                cancellationToken: ct))
                results.Add(e);
            return results;
        }

        public async Task<List<QueueConfigEntity>> GetAllQueuesAsync(
            CancellationToken ct = default)
        {
            var table   = _client.GetTableClient(ConfigTable);
            var results = new List<QueueConfigEntity>();
            await foreach (var e in table.QueryAsync<QueueConfigEntity>(
                filter: "PartitionKey eq 'config'",
                cancellationToken: ct))
                results.Add(e);
            return results;
        }

        public async Task<QueueConfigEntity?> GetQueueConfigAsync(
            string queueName, CancellationToken ct = default)
        {
            var table = _client.GetTableClient(ConfigTable);
            try
            {
                var response = await table.GetEntityAsync<QueueConfigEntity>(
                    "config", queueName, cancellationToken: ct);
                return response.Value;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
        }

        public async Task SaveConfigAsync(
            QueueConfigEntity config, CancellationToken ct = default)
        {
            var table = _client.GetTableClient(ConfigTable);
            await table.UpsertEntityAsync(config, TableUpdateMode.Replace, ct);
        }

        public async Task SaveSnapshotAsync(
            QueueSnapshotEntity snapshot, CancellationToken ct = default)
        {
            var table = _client.GetTableClient(SnapshotTable);
            await table.UpsertEntityAsync(snapshot, TableUpdateMode.Replace, ct);
        }

        public async Task<List<QueueSnapshotEntity>> GetRecentSnapshotsAsync(
            string queueName, int count, CancellationToken ct = default)
        {
            var table   = _client.GetTableClient(SnapshotTable);
            var results = new List<QueueSnapshotEntity>();
            await foreach (var e in table.QueryAsync<QueueSnapshotEntity>(
                filter: $"PartitionKey eq '{queueName}'",
                maxPerPage: count,
                cancellationToken: ct))
            {
                results.Add(e);
                if (results.Count >= count) break;
            }
            return results;
        }

        public async Task SaveStatusAsync(
            QueueStatusEntity status, CancellationToken ct = default)
        {
            var table = _client.GetTableClient(StatusTable);
            await table.UpsertEntityAsync(status, TableUpdateMode.Replace, ct);
        }

        public async Task<List<QueueStatusEntity>> GetRecentStatusAsync(
            string queueName, int count, CancellationToken ct = default)
        {
            var table   = _client.GetTableClient(StatusTable);
            var results = new List<QueueStatusEntity>();
            await foreach (var e in table.QueryAsync<QueueStatusEntity>(
                filter: $"PartitionKey eq '{queueName}'",
                maxPerPage: count,
                cancellationToken: ct))
            {
                results.Add(e);
                if (results.Count >= count) break;
            }
            return results;
        }

        public async Task<QueueStatusEntity?> GetLatestStatusAsync(
            string queueName, CancellationToken ct = default)
        {
            var results = await GetRecentStatusAsync(queueName, 1, ct);
            return results.FirstOrDefault();
        }

        public async Task<List<QueueStatusEntity>> GetStatusHistoryAsync(
            string queueName, DateTime from, DateTime to, CancellationToken ct = default)
        {
            // RowKey = MaxTicks - T.Ticks → smaller = more recent
            // For [from, to]: lowerRowKey = MaxTicks - to.Ticks, upperRowKey = MaxTicks - from.Ticks
            var lowerRowKey = (DateTime.MaxValue.Ticks - to.Ticks).ToString("D19");
            var upperRowKey = (DateTime.MaxValue.Ticks - from.Ticks).ToString("D19");
            var table   = _client.GetTableClient(StatusTable);
            var results = new List<QueueStatusEntity>();
            await foreach (var e in table.QueryAsync<QueueStatusEntity>(
                filter: $"PartitionKey eq '{queueName}' and RowKey ge '{lowerRowKey}' and RowKey le '{upperRowKey}'",
                cancellationToken: ct))
                results.Add(e);
            return results;
        }

        public async Task<List<QueueSnapshotEntity>> GetSnapshotHistoryAsync(
            string queueName, DateTime from, DateTime to, CancellationToken ct = default)
        {
            var lowerRowKey = (DateTime.MaxValue.Ticks - to.Ticks).ToString("D19");
            var upperRowKey = (DateTime.MaxValue.Ticks - from.Ticks).ToString("D19");
            var table   = _client.GetTableClient(SnapshotTable);
            var results = new List<QueueSnapshotEntity>();
            await foreach (var e in table.QueryAsync<QueueSnapshotEntity>(
                filter: $"PartitionKey eq '{queueName}' and RowKey ge '{lowerRowKey}' and RowKey le '{upperRowKey}'",
                cancellationToken: ct))
                results.Add(e);
            return results;
        }

        public async Task<List<AlertRecordEntity>> GetAlertHistoryAsync(
            string queueName, CancellationToken ct = default)
        {
            var table   = _client.GetTableClient(AlertTable);
            var results = new List<AlertRecordEntity>();
            await foreach (var e in table.QueryAsync<AlertRecordEntity>(
                filter: $"PartitionKey eq '{queueName}'",
                cancellationToken: ct))
                results.Add(e);
            return results.OrderByDescending(a => a.OpenedAtUtc).ToList();
        }

        public async Task<AlertRecordEntity?> GetOpenAlertAsync(
            string queueName, CancellationToken ct = default)
        {
            var table   = _client.GetTableClient(AlertTable);
            var results = new List<AlertRecordEntity>();
            await foreach (var e in table.QueryAsync<AlertRecordEntity>(
                filter: $"PartitionKey eq '{queueName}' and Status eq 'Open'",
                cancellationToken: ct))
            {
                results.Add(e);
                break;
            }
            return results.FirstOrDefault();
        }

        public async Task SaveAlertRecordAsync(
            AlertRecordEntity alert, CancellationToken ct = default)
        {
            var table = _client.GetTableClient(AlertTable);
            await table.UpsertEntityAsync(alert, TableUpdateMode.Replace, ct);
        }

        public async Task DeleteQueueConfigAsync(
            string partitionKey, string rowKey, CancellationToken ct = default)
        {
            var table = _client.GetTableClient(ConfigTable);
            await table.DeleteEntityAsync(partitionKey, rowKey, Azure.ETag.All, ct);
        }

        public Task<int> PurgeOldSnapshotsAsync(
            string queueName, DateTime cutoff, CancellationToken ct = default)
        {
            // Reverse-tick RowKey: older rows have higher RowKey values.
            var cutoffRowKey = (DateTime.MaxValue.Ticks - cutoff.Ticks).ToString("D19");
            return PurgeByRowKeyAsync(SnapshotTable, queueName, cutoffRowKey, ct);
        }

        public Task<int> PurgeOldStatusAsync(
            string queueName, DateTime cutoff, CancellationToken ct = default)
        {
            var cutoffRowKey = (DateTime.MaxValue.Ticks - cutoff.Ticks).ToString("D19");
            return PurgeByRowKeyAsync(StatusTable, queueName, cutoffRowKey, ct);
        }

        public async Task<int> PurgeOldAlertRecordsAsync(
            string queueName, DateTime cutoff, CancellationToken ct = default)
        {
            // AlertRecord uses a GUID RowKey — filter on OpenedAtUtc property instead.
            var table = _client.GetTableClient(AlertTable);
            var cutoffStr = cutoff.ToString("yyyy-MM-ddTHH:mm:ssZ");
            var actions = new List<TableTransactionAction>();
            await foreach (var e in table.QueryAsync<TableEntity>(
                filter: $"PartitionKey eq '{queueName}' and OpenedAtUtc lt datetime'{cutoffStr}'",
                select: new[] { "PartitionKey", "RowKey" },
                cancellationToken: ct))
            {
                actions.Add(new TableTransactionAction(TableTransactionActionType.Delete, e));
            }
            return await BatchDeleteAsync(table, actions, ct);
        }

        // Deletes all rows in tableName/partitionKey whose RowKey is greater than cutoffRowKey.
        // In the reverse-tick scheme, RowKey > cutoffRowKey means the row is older than the cutoff.
        private async Task<int> PurgeByRowKeyAsync(
            string tableName, string partitionKey, string cutoffRowKey, CancellationToken ct)
        {
            var table   = _client.GetTableClient(tableName);
            var actions = new List<TableTransactionAction>();
            await foreach (var e in table.QueryAsync<TableEntity>(
                filter: $"PartitionKey eq '{partitionKey}' and RowKey gt '{cutoffRowKey}'",
                select: new[] { "PartitionKey", "RowKey" },
                cancellationToken: ct))
            {
                actions.Add(new TableTransactionAction(TableTransactionActionType.Delete, e));
            }
            return await BatchDeleteAsync(table, actions, ct);
        }

        // Submits delete actions in batches of 100 (Azure Table Storage transaction limit).
        private static async Task<int> BatchDeleteAsync(
            TableClient table, List<TableTransactionAction> actions, CancellationToken ct)
        {
            int deleted = 0;
            for (int i = 0; i < actions.Count; i += 100)
            {
                var batch = actions.Skip(i).Take(100).ToList();
                await table.SubmitTransactionAsync(batch, ct);
                deleted += batch.Count;
            }
            return deleted;
        }
    }
}
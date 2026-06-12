using QueueBacklogIntelligence.Models;

namespace QueueBacklogIntelligence.Services
{
    public interface IRepository
    {
        Task EnsureTablesExistAsync(CancellationToken ct = default);

        // Config
        Task<List<QueueConfigEntity>> GetEnabledQueuesAsync(
            CancellationToken ct = default);
        Task<List<QueueConfigEntity>> GetAllQueuesAsync(
            CancellationToken ct = default);
        Task<QueueConfigEntity?> GetQueueConfigAsync(
            string queueName, CancellationToken ct = default);
        Task SaveConfigAsync(
            QueueConfigEntity config, CancellationToken ct = default);

        // Snapshots
        Task SaveSnapshotAsync(
            QueueSnapshotEntity snapshot, CancellationToken ct = default);
        Task<List<QueueSnapshotEntity>> GetRecentSnapshotsAsync(
            string queueName, int count, CancellationToken ct = default);

        // Status
        Task SaveStatusAsync(
            QueueStatusEntity status, CancellationToken ct = default);
        Task<List<QueueStatusEntity>> GetRecentStatusAsync(
            string queueName, int count, CancellationToken ct = default);
        Task<QueueStatusEntity?> GetLatestStatusAsync(
            string queueName, CancellationToken ct = default);

        // History queries (time-window based)
        Task<List<QueueStatusEntity>> GetStatusHistoryAsync(
            string queueName, DateTime from, DateTime to, CancellationToken ct = default);
        Task<List<QueueSnapshotEntity>> GetSnapshotHistoryAsync(
            string queueName, DateTime from, DateTime to, CancellationToken ct = default);
        Task<List<AlertRecordEntity>> GetAlertHistoryAsync(
            string queueName, CancellationToken ct = default);

        // Alert records
        Task<AlertRecordEntity?> GetOpenAlertAsync(
            string queueName, CancellationToken ct = default);
        Task SaveAlertRecordAsync(
            AlertRecordEntity alert, CancellationToken ct = default);

        // Queue config management (Settings page)
        Task DeleteQueueConfigAsync(
            string partitionKey, string rowKey, CancellationToken ct = default);
    }
}
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using QueueBacklogIntelligence.Services;

namespace QueueBacklogIntelligence.Functions
{
    public class CleanupFunction
    {
        private readonly IRepository             _repository;
        private readonly ILogger<CleanupFunction> _logger;

        private const int SnapshotRetentionHours = 24;
        private const int StatusRetentionDays    = 90;
        private const int AlertRetentionDays     = 90;

        public CleanupFunction(IRepository repository, ILogger<CleanupFunction> logger)
        {
            _repository = repository;
            _logger     = logger;
        }

        // Runs daily at 02:00 UTC
        [Function("CleanupFunction")]
        public async Task Run(
            [TimerTrigger("0 0 2 * * *")] TimerInfo timer,
            CancellationToken ct)
        {
            _logger.LogInformation("CleanupFunction started at {Time:O}", DateTime.UtcNow);

            var queues = await _repository.GetAllQueuesAsync(ct);
            if (queues.Count == 0)
            {
                _logger.LogInformation("No queues configured — nothing to clean up.");
                return;
            }

            var snapshotCutoff = DateTime.UtcNow.AddHours(-SnapshotRetentionHours);
            var statusCutoff   = DateTime.UtcNow.AddDays(-StatusRetentionDays);
            var alertCutoff    = DateTime.UtcNow.AddDays(-AlertRetentionDays);

            int totalSnapshots = 0, totalStatus = 0, totalAlerts = 0;

            foreach (var queue in queues)
            {
                try
                {
                    int s = await _repository.PurgeOldSnapshotsAsync(queue.QueueName, snapshotCutoff, ct);
                    int st = await _repository.PurgeOldStatusAsync(queue.QueueName, statusCutoff, ct);
                    int a = await _repository.PurgeOldAlertRecordsAsync(queue.QueueName, alertCutoff, ct);

                    totalSnapshots += s;
                    totalStatus    += st;
                    totalAlerts    += a;

                    _logger.LogInformation(
                        "Queue '{Q}': deleted {S} snapshots, {St} status rows, {A} alert records.",
                        queue.QueueName, s, st, a);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Cleanup failed for queue '{Q}'.", queue.QueueName);
                }
            }

            _logger.LogInformation(
                "CleanupFunction done. Total deleted — snapshots: {S}, status: {St}, alerts: {A}.",
                totalSnapshots, totalStatus, totalAlerts);
        }
    }
}

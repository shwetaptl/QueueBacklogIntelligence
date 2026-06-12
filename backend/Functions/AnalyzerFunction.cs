using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using QueueBacklogIntelligence.Services;

namespace QueueBacklogIntelligence.Functions
{
    public class AnalyzerFunction
    {
        private readonly IRepository               _repository;
        private readonly AnalyzerService           _analyzer;
        private readonly ILogger<AnalyzerFunction> _logger;

        public AnalyzerFunction(
            IRepository repository,
            AnalyzerService analyzer,
            ILogger<AnalyzerFunction> logger)
        {
            _repository = repository;
            _analyzer   = analyzer;
            _logger     = logger;
        }

        // Runs every minute at :30 seconds
        // 30 seconds after Collector ensures snapshot is written first
        [Function("AnalyzerFunction")]
        public async Task Run(
            [TimerTrigger("30 */1 * * * *")] TimerInfo timer,
            CancellationToken ct)
        {
            _logger.LogInformation(
                "AnalyzerFunction started at {Time:O}", DateTime.UtcNow);

            var queues = await _repository.GetEnabledQueuesAsync(ct);

            if (queues.Count == 0)
            {
                _logger.LogWarning("No enabled queues found.");
                return;
            }

            // Process all queues in parallel
            var tasks = queues.Select(async config =>
            {
                try
                {
                    var snapshots = await _repository.GetRecentSnapshotsAsync(
                        config.QueueName, AnalyzerService.SnapshotCount, ct);

                    var recentStatus = await _repository.GetRecentStatusAsync(
                        config.QueueName, config.SlaMinutes + 5, ct);

                    var status = _analyzer.Analyze(
                        config, snapshots, recentStatus);

                    await _repository.SaveStatusAsync(status, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Analyzer failed for queue '{Queue}'.",
                        config.QueueName);
                }
            });

            await Task.WhenAll(tasks);

            _logger.LogInformation(
                "AnalyzerFunction done. {Count} queue(s) analyzed.",
                queues.Count);
        }
    }
}
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using QueueBacklogIntelligence.Services;

namespace QueueBacklogIntelligence.Functions
{
    public class CollectorFunction
    {
        private readonly IRepository           _repository;
        private readonly CollectorService      _collector;
        private readonly ILogger<CollectorFunction> _logger;

        public CollectorFunction(
            IRepository repository,
            CollectorService collector,
            ILogger<CollectorFunction> logger)
        {
            _repository = repository;
            _collector  = collector;
            _logger     = logger;
        }

        // Runs every minute at :00 seconds
        [Function("CollectorFunction")]
        public async Task Run(
            [TimerTrigger("0 */1 * * * *")] TimerInfo timer,
            CancellationToken ct)
        {
            var runTime = DateTime.UtcNow;
            _logger.LogInformation(
                "CollectorFunction started at {Time:O}", runTime);

            var queues = await _repository.GetEnabledQueuesAsync(ct);
            if (queues.Count == 0)
            {
                _logger.LogWarning("No enabled queues in QueueConfig.");
                return;
            }

            // Process all queues in parallel
            var tasks = queues.Select(async config =>
            {
                try
                {
                    var snapshot = await _collector.CollectAsync(
                        config, runTime, ct);
                    await _repository.SaveSnapshotAsync(snapshot, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Collector failed for queue '{Queue}'.",
                        config.QueueName);
                }
            });

            await Task.WhenAll(tasks);
            _logger.LogInformation(
                "CollectorFunction done. {Count} queue(s).", queues.Count);
        }
    }
}
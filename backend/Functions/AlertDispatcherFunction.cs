using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using QueueBacklogIntelligence.Services;

namespace QueueBacklogIntelligence.Functions
{
    public class AlertDispatcherFunction
    {
        private readonly IRepository                    _repository;
        private readonly AlertService                   _alertService;
        private readonly ILogger<AlertDispatcherFunction> _logger;

        public AlertDispatcherFunction(
            IRepository repository,
            AlertService alertService,
            ILogger<AlertDispatcherFunction> logger)
        {
            _repository   = repository;
            _alertService = alertService;
            _logger       = logger;
        }

        // Runs every minute at :45 — 15 seconds after Analyzer writes status at :30
        [Function("AlertDispatcherFunction")]
        public async Task Run(
            [TimerTrigger("45 * * * * *")] TimerInfo timer,
            CancellationToken ct)
        {
            _logger.LogInformation(
                "AlertDispatcherFunction started at {Time:O}", DateTime.UtcNow);

            var queues = await _repository.GetEnabledQueuesAsync(ct);

            if (queues.Count == 0)
            {
                _logger.LogWarning("No enabled queues found.");
                return;
            }

            var tasks = queues.Select(async config =>
            {
                try
                {
                    var status = await _repository.GetLatestStatusAsync(config.QueueName, ct);
                    if (status == null)
                    {
                        _logger.LogWarning(
                            "Queue '{Q}': No status row found. Skipping alert check.",
                            config.QueueName);
                        return;
                    }

                    await _alertService.ProcessAsync(config, status, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "AlertDispatcher failed for queue '{Queue}'.",
                        config.QueueName);
                }
            });

            await Task.WhenAll(tasks);

            _logger.LogInformation(
                "AlertDispatcherFunction done. {Count} queue(s) checked.",
                queues.Count);
        }
    }
}

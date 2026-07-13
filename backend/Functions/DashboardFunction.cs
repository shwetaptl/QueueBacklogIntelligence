using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using QueueBacklogIntelligence.Services;

namespace QueueBacklogIntelligence.Functions
{
    // ── Request / Response DTOs (public so Swagger can reflect on them) ───────

    public record QueueConfigRequest(
        string  QueueName,
        string  Namespace,
        string  SubscriptionId,
        string  ResourceGroupName,
        int     SlaMinutes,
        bool    IsEnabled,
        int     CooldownMinutes,
        double  WarningThreshold,
        double  CriticalThreshold,
        string? TeamsWebhookUrl,
        string? EmailRecipients
    );

    public record QueueSummaryResponse(
        string   QueueName,
        bool     IsEnabled,
        int      SlaMinutes,
        int      ActiveCount,
        double?  WaitTimeMinutes,
        string   SlaStatus,
        string   TrendLabel,
        string   RootCause,
        string   AlertSeverity,
        double   GapPerMin,
        double   Acceleration,
        DateTime? LastUpdatedUtc
    );

    public record HealthResponse(
        string    Status,
        DateTime? LastCollectorRunUtc,
        DateTime? LastAnalyzerRunUtc,
        double?   CollectorDelayMinutes,
        double?   AnalyzerDelayMinutes,
        string    Message
    );

    public record MessageResponse(string Message);
    public record ErrorResponse(string Error);

    // ─────────────────────────────────────────────────────────────────────────

    public class DashboardFunction
    {
        private readonly IRepository              _repository;
        private readonly ILogger<DashboardFunction> _logger;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public DashboardFunction(IRepository repository, ILogger<DashboardFunction> logger)
        {
            _repository = repository;
            _logger     = logger;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void AddCors(HttpRequest req)
        {
            req.HttpContext.Response.Headers["Access-Control-Allow-Origin"]  = "*";
            req.HttpContext.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS";
            req.HttpContext.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
        }

        private static ContentResult Json(object data) => new()
        {
            Content     = JsonSerializer.Serialize(data, _json),
            ContentType = "application/json",
            StatusCode  = 200,
        };

        private static (DateTime from, DateTime to) ParseTimeRange(HttpRequest req)
        {
            if (!string.IsNullOrEmpty(req.Query["from"]) && !string.IsNullOrEmpty(req.Query["to"]) &&
                DateTime.TryParse(req.Query["from"], null,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var from) &&
                DateTime.TryParse(req.Query["to"], null,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var to))
            {
                if ((to - from).TotalDays > 90) from = to.AddDays(-90);
                return (from, to);
            }
            int minutes = int.TryParse(req.Query["minutes"], out int m) ? Math.Clamp(m, 1, 10080) : 30;
            return (DateTime.UtcNow.AddMinutes(-minutes), DateTime.UtcNow);
        }


        // ── OPTIONS /* — CORS preflight ───────────────────────────────────────

        [Function("CorsOptions")]
        public IActionResult HandleOptions(
            [HttpTrigger(AuthorizationLevel.Anonymous, "options", Route = "{*route}")]
            HttpRequest req)
        {
            AddCors(req);
            return new OkResult();
        }


        // ── GET /api/queues ───────────────────────────────────────────────────

        [Function("GetQueues")]
        public async Task<IActionResult> GetQueues(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "queues")]
            HttpRequest req,
            CancellationToken ct)
        {
            AddCors(req);
            var configs = await _repository.GetAllQueuesAsync(ct);

            var rows = await Task.WhenAll(configs.Select(async c =>
            {
                var s = await _repository.GetLatestStatusAsync(c.QueueName, ct);
                return new
                {
                    // ── Config fields (needed by Settings page) ──────────────
                    queueName         = c.QueueName,
                    @namespace        = c.Namespace,
                    subscriptionId    = c.SubscriptionId,
                    resourceGroupName = c.ResourceGroupName,
                    slaMinutes        = c.SlaMinutes,
                    isEnabled         = c.IsEnabled,
                    cooldownMinutes   = c.CooldownMinutes,
                    warningThreshold  = c.WarningThreshold,
                    criticalThreshold = c.CriticalThreshold,
                    teamsWebhookUrl   = c.TeamsWebhookUrl,
                    emailRecipients   = c.EmailRecipients,
                    // ── Live status fields (needed by Dashboard) ─────────────
                    activeCount     = s?.ActiveCount     ?? 0,
                    waitTimeMinutes = s?.WaitTimeMinutes,
                    slaStatus       = s?.SlaStatus       ?? "UNKNOWN",
                    trendLabel      = s?.TrendLabel      ?? "Unknown",
                    rootCause       = s?.RootCause       ?? "Unknown",
                    alertSeverity   = s?.AlertSeverity   ?? "None",
                    dlqCount        = s?.DLQCount        ?? 0,
                    gapPerMin       = s?.GapPerMin       ?? 0.0,
                    acceleration    = s?.Acceleration    ?? 0.0,
                    lastUpdatedUtc  = s?.TimestampUtc,
                };
            }));

            return Json(rows);
        }


        // ── GET /api/queues/{queueName}/status ────────────────────────────────

        [Function("GetQueueStatus")]
        public async Task<IActionResult> GetQueueStatus(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "queues/{queueName}/status")]
            HttpRequest req,
            string queueName,
            CancellationToken ct)
        {
            AddCors(req);
            var allConfigs = await _repository.GetAllQueuesAsync(ct);
            var config = allConfigs.FirstOrDefault(c => c.QueueName == queueName);
            if (config == null) return new NotFoundResult();

            var s = await _repository.GetLatestStatusAsync(queueName, ct);
            return Json(new
            {
                queueName       = queueName,
                isEnabled       = config.IsEnabled,
                slaMinutes      = config.SlaMinutes,
                activeCount     = s?.ActiveCount     ?? 0,
                waitTimeMinutes = s?.WaitTimeMinutes,
                slaStatus       = s?.SlaStatus       ?? "UNKNOWN",
                trendLabel      = s?.TrendLabel      ?? "Unknown",
                rootCause       = s?.RootCause       ?? "Unknown",
                alertSeverity   = s?.AlertSeverity   ?? "None",
                gapPerMin       = s?.GapPerMin       ?? 0.0,
                acceleration    = s?.Acceleration    ?? 0.0,
                dlqCount        = s?.DLQCount        ?? 0,
                incomingPerMin  = s?.IncomingPerMin,
                outgoingPerMin  = s?.OutgoingPerMin,
                lastUpdatedUtc  = s?.TimestampUtc,
            });
        }


        // ── GET /api/queues/{queueName}/history?minutes=30 ────────────────────

        [Function("GetQueueHistory")]
        public async Task<IActionResult> GetQueueHistory(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "queues/{queueName}/history")]
            HttpRequest req,
            string queueName,
            CancellationToken ct)
        {
            AddCors(req);
            var (from, to) = ParseTimeRange(req);
            var rows = await _repository.GetStatusHistoryAsync(queueName, from, to, ct);

            // Oldest first — natural order for time-series charts
            return Json(rows
                .OrderBy(s => s.TimestampUtc)
                .Select(s => new
                {
                    timestampUtc    = s.TimestampUtc,
                    activeCount     = s.ActiveCount,
                    waitTimeMinutes = s.WaitTimeMinutes,
                    slaStatus       = s.SlaStatus,
                    alertSeverity   = s.AlertSeverity,
                    trendLabel      = s.TrendLabel,
                    rootCause       = s.RootCause,
                    gapPerMin       = s.GapPerMin,
                    incomingPerMin  = s.IncomingPerMin,
                    outgoingPerMin  = s.OutgoingPerMin,
                }));
        }


        // ── GET /api/queues/{queueName}/snapshots?minutes=30 ─────────────────

        [Function("GetQueueSnapshots")]
        public async Task<IActionResult> GetQueueSnapshots(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "queues/{queueName}/snapshots")]
            HttpRequest req,
            string queueName,
            CancellationToken ct)
        {
            AddCors(req);
            var (from, to) = ParseTimeRange(req);
            var rows = await _repository.GetSnapshotHistoryAsync(queueName, from, to, ct);

            return Json(rows
                .OrderBy(s => s.TimestampUtc)
                .Select(s => new
                {
                    timestampUtc   = s.TimestampUtc,
                    activeCount    = s.ActiveCount,
                    incomingPerMin = s.IncomingPerMin,
                    outgoingPerMin = s.OutgoingPerMin,
                    dlqCount       = s.DLQCount,
                }));
        }


        // ── GET /api/queues/{queueName}/alerts ────────────────────────────────

        [Function("GetQueueAlerts")]
        public async Task<IActionResult> GetQueueAlerts(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "queues/{queueName}/alerts")]
            HttpRequest req,
            string queueName,
            CancellationToken ct)
        {
            AddCors(req);
            var rows = await _repository.GetAlertHistoryAsync(queueName, ct);
            var now  = DateTime.UtcNow;

            return Json(rows.Select(a => new
            {
                incidentId      = a.IncidentId,
                openedAtUtc     = a.OpenedAtUtc,
                resolvedAtUtc   = a.ResolvedAtUtc,
                status          = a.Status,
                peakSeverity    = a.PeakSeverity,
                firstRootCause  = a.FirstRootCause,
                alertCount      = a.AlertCount,
                durationMinutes = (int)(a.ResolvedAtUtc.HasValue
                    ? (a.ResolvedAtUtc.Value - a.OpenedAtUtc).TotalMinutes
                    : (now - a.OpenedAtUtc).TotalMinutes),
            }));
        }


        // ── POST /api/queues — create queue config ────────────────────────────

        [Function("CreateQueue")]
        public async Task<IActionResult> CreateQueue(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "queues")]
            HttpRequest req,
            CancellationToken ct)
        {
            AddCors(req);
            QueueConfigRequest? body;
            try
            {
                body = await JsonSerializer.DeserializeAsync<QueueConfigRequest>(
                    req.Body, _json, ct);
            }
            catch { return new BadRequestObjectResult(new { error = "Invalid JSON body" }); }

            if (body is null || string.IsNullOrWhiteSpace(body.QueueName))
                return new BadRequestObjectResult(new { error = "QueueName is required" });

            // Check not already existing
            var existing = await _repository.GetAllQueuesAsync(ct);
            if (existing.Any(c => c.QueueName == body.QueueName))
                return new ConflictObjectResult(new { error = $"Queue '{body.QueueName}' already exists" });

            var config = new Models.QueueConfigEntity
            {
                PartitionKey      = "config",
                RowKey            = body.QueueName,
                QueueName         = body.QueueName,
                Namespace         = body.Namespace,
                SubscriptionId    = body.SubscriptionId,
                ResourceGroupName = body.ResourceGroupName,
                SlaMinutes        = body.SlaMinutes,
                IsEnabled         = body.IsEnabled,
                CooldownMinutes   = body.CooldownMinutes,
                WarningThreshold  = body.WarningThreshold,
                CriticalThreshold = body.CriticalThreshold,
                TeamsWebhookUrl   = body.TeamsWebhookUrl,
                EmailRecipients   = body.EmailRecipients,
            };

            await _repository.SaveConfigAsync(config, ct);
            return new OkObjectResult(new { message = $"Queue '{body.QueueName}' created" });
        }


        // ── PUT /api/queues/{queueName} — update queue config ─────────────────

        [Function("UpdateQueue")]
        public async Task<IActionResult> UpdateQueue(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "queues/{queueName}")]
            HttpRequest req,
            string queueName,
            CancellationToken ct)
        {
            AddCors(req);
            var all      = await _repository.GetAllQueuesAsync(ct);
            var existing = all.FirstOrDefault(c => c.QueueName == queueName);
            if (existing is null)
                return new NotFoundObjectResult(new { error = $"Queue '{queueName}' not found" });

            QueueConfigRequest? body;
            try
            {
                body = await JsonSerializer.DeserializeAsync<QueueConfigRequest>(
                    req.Body, _json, ct);
            }
            catch { return new BadRequestObjectResult(new { error = "Invalid JSON body" }); }

            if (body is null)
                return new BadRequestObjectResult(new { error = "Request body required" });

            // Use the existing entity's PartitionKey + RowKey so UpsertEntityAsync
            // updates in-place rather than creating a new row.
            var config = new Models.QueueConfigEntity
            {
                PartitionKey      = existing.PartitionKey,
                RowKey            = existing.RowKey,
                QueueName         = queueName,
                Namespace         = body.Namespace,
                SubscriptionId    = body.SubscriptionId,
                ResourceGroupName = body.ResourceGroupName,
                SlaMinutes        = body.SlaMinutes,
                IsEnabled         = body.IsEnabled,
                CooldownMinutes   = body.CooldownMinutes,
                WarningThreshold  = body.WarningThreshold,
                CriticalThreshold = body.CriticalThreshold,
                TeamsWebhookUrl   = body.TeamsWebhookUrl,
                EmailRecipients   = body.EmailRecipients,
            };

            await _repository.SaveConfigAsync(config, ct);
            return new OkObjectResult(new { message = $"Queue '{queueName}' updated" });
        }


        // ── DELETE /api/queues/{queueName} — remove queue config ──────────────

        [Function("DeleteQueue")]
        public async Task<IActionResult> DeleteQueue(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "queues/{queueName}")]
            HttpRequest req,
            string queueName,
            CancellationToken ct)
        {
            AddCors(req);
            var all      = await _repository.GetAllQueuesAsync(ct);
            var existing = all.FirstOrDefault(c => c.QueueName == queueName);
            if (existing is null)
                return new NotFoundObjectResult(new { error = $"Queue '{queueName}' not found" });

            await _repository.DeleteQueueConfigAsync(existing.PartitionKey, existing.RowKey, ct);
            return new OkObjectResult(new { message = $"Queue '{queueName}' deleted" });
        }


        // ── GET /api/health ───────────────────────────────────────────────────

        [Function("GetHealth")]
        public async Task<IActionResult> GetHealth(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")]
            HttpRequest req,
            CancellationToken ct)
        {
            AddCors(req);
            var now    = DateTime.UtcNow;
            var queues = await _repository.GetAllQueuesAsync(ct);

            if (queues.Count == 0)
            {
                return Json(new
                {
                    status                = "Degraded",
                    lastCollectorRunUtc   = (DateTime?)null,
                    lastAnalyzerRunUtc    = (DateTime?)null,
                    collectorDelayMinutes = (double?)null,
                    analyzerDelayMinutes  = (double?)null,
                    message               = "No queues configured",
                });
            }

            // Parallel fetch of latest snapshot + status per queue
            var snapshotTimes = await Task.WhenAll(queues.Select(async q =>
            {
                var snaps = await _repository.GetRecentSnapshotsAsync(q.QueueName, 1, ct);
                return snaps.FirstOrDefault()?.TimestampUtc;
            }));
            var statusTimes = await Task.WhenAll(queues.Select(async q =>
            {
                var s = await _repository.GetLatestStatusAsync(q.QueueName, ct);
                return s?.TimestampUtc;
            }));

            var lastCollector = snapshotTimes
                .Where(t => t.HasValue).Select(t => t!.Value)
                .DefaultIfEmpty(DateTime.MinValue).Max();
            var lastAnalyzer = statusTimes
                .Where(t => t.HasValue).Select(t => t!.Value)
                .DefaultIfEmpty(DateTime.MinValue).Max();

            bool noCollector = lastCollector == DateTime.MinValue;
            bool noAnalyzer  = lastAnalyzer  == DateTime.MinValue;

            double collectorDelay = noCollector ? double.MaxValue : (now - lastCollector).TotalMinutes;
            double analyzerDelay  = noAnalyzer  ? double.MaxValue : (now - lastAnalyzer).TotalMinutes;
            bool   degraded       = collectorDelay > 3 || analyzerDelay > 3;

            return Json(new
            {
                status                = degraded ? "Degraded" : "Healthy",
                lastCollectorRunUtc   = noCollector ? (DateTime?)null : lastCollector,
                lastAnalyzerRunUtc    = noAnalyzer  ? (DateTime?)null : lastAnalyzer,
                collectorDelayMinutes = noCollector ? (double?)null : Math.Round(collectorDelay, 1),
                analyzerDelayMinutes  = noAnalyzer  ? (double?)null : Math.Round(analyzerDelay, 1),
                message               = degraded
                    ? "One or more functions appear to have stopped running"
                    : "All systems operational",
            });
        }
    }
}

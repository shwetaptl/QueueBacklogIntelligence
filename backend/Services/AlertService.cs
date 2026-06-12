using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using QueueBacklogIntelligence.Models;

namespace QueueBacklogIntelligence.Services
{
    public class AlertService
    {
        private readonly IRepository             _repository;
        private readonly IHttpClientFactory      _httpClientFactory;
        private readonly ILogger<AlertService>   _logger;

        private enum AlertType { NewIncident, Escalation, Reminder, Recovery }

        public AlertService(
            IRepository repository,
            IHttpClientFactory httpClientFactory,
            ILogger<AlertService> logger)
        {
            _repository        = repository;
            _httpClientFactory = httpClientFactory;
            _logger            = logger;
        }

        public async Task ProcessAsync(
            QueueConfigEntity config,
            QueueStatusEntity status,
            CancellationToken ct)
        {
            if (!status.NeedToSendAlert) return;

            if (string.IsNullOrWhiteSpace(config.TeamsWebhookUrl))
            {
                _logger.LogWarning(
                    "Queue '{Q}': TeamsWebhookUrl not configured. Skipping alert.",
                    config.QueueName);
                return;
            }

            try
            {
                var openAlert = await _repository.GetOpenAlertAsync(config.QueueName, ct);
                var alertType = DetermineAlertType(status, openAlert);

                var payload = BuildTeamsCard(config, status, alertType, openAlert);
                await PostToTeamsAsync(config.TeamsWebhookUrl, payload, config.QueueName, ct);
                await UpdateAlertRecordAsync(config, status, alertType, openAlert, ct);

                _logger.LogInformation(
                    "Queue '{Q}': Alert dispatched — type={T} severity={S}.",
                    config.QueueName, alertType, status.AlertSeverity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Queue '{Q}': Alert processing failed. Alert failure will not block next run.",
                    config.QueueName);
            }
        }


        // ── Alert type determination ───────────────────────────────────────────

        private static AlertType DetermineAlertType(
            QueueStatusEntity status,
            AlertRecordEntity? openAlert)
        {
            if (status.AlertSeverity == "None" && openAlert != null)
                return AlertType.Recovery;

            if (openAlert == null)
                return AlertType.NewIncident;

            if (openAlert.PeakSeverity == "Warning" && status.AlertSeverity == "Critical")
                return AlertType.Escalation;

            return AlertType.Reminder;
        }


        // ── Teams Adaptive Card builder ────────────────────────────────────────

        private static string BuildTeamsCard(
            QueueConfigEntity config,
            QueueStatusEntity status,
            AlertType alertType,
            AlertRecordEntity? openAlert)
        {
            string title = alertType switch
            {
                AlertType.NewIncident when status.AlertSeverity == "Critical"
                    => $"🔴 CRITICAL — {config.QueueName}",
                AlertType.NewIncident
                    => $"🟡 WARNING — {config.QueueName}",
                AlertType.Escalation
                    => $"🔴 ESCALATED TO CRITICAL — {config.QueueName}",
                AlertType.Reminder when status.AlertSeverity == "Critical"
                    => $"🔴 STILL CRITICAL — {config.QueueName} (reminder)",
                AlertType.Reminder
                    => $"🟡 STILL WARNING — {config.QueueName} (reminder)",
                AlertType.Recovery
                    => $"✅ RESOLVED — {config.QueueName}",
                _   => config.QueueName,
            };

            var body = new JsonArray();
            body.Add(new JsonObject
            {
                ["type"]   = "TextBlock",
                ["size"]   = "Large",
                ["weight"] = "Bolder",
                ["text"]   = title,
                ["wrap"]   = true,
            });

            if (alertType == AlertType.Recovery)
            {
                string duration = openAlert != null
                    ? $"{(int)(DateTime.UtcNow - openAlert.OpenedAtUtc).TotalMinutes} minutes"
                    : "unknown";
                body.Add(new JsonObject
                {
                    ["type"] = "TextBlock",
                    ["text"] = $"Queue has recovered. Incident duration: {duration}.",
                    ["wrap"] = true,
                });
            }
            else
            {
                string waitDisplay = status.WaitTimeMinutes.HasValue
                    ? $"{status.WaitTimeMinutes:F1} min (SLA: {status.SlaMinutes} min)"
                    : $"∞  — consumer stopped (SLA: {status.SlaMinutes} min)";

                var facts = new JsonArray
                {
                    new JsonObject { ["title"] = "Root Cause",      ["value"] = status.RootCause },
                    new JsonObject { ["title"] = "Active Messages", ["value"] = status.ActiveCount.ToString() },
                    new JsonObject { ["title"] = "Wait Time",       ["value"] = waitDisplay },
                    new JsonObject { ["title"] = "Trend",           ["value"] = status.TrendLabel },
                };
                body.Add(new JsonObject { ["type"] = "FactSet", ["facts"] = facts });
            }

            var card = new JsonObject
            {
                ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
                ["type"]    = "AdaptiveCard",
                ["version"] = "1.4",
                ["body"]    = body,
            };

            var payload = new JsonObject
            {
                ["type"] = "message",
                ["attachments"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["contentType"] = "application/vnd.microsoft.card.adaptive",
                        ["contentUrl"]  = null,
                        ["content"]     = card,
                    }
                }
            };

            return payload.ToJsonString();
        }


        // ── HTTP dispatch ──────────────────────────────────────────────────────

        private async Task PostToTeamsAsync(
            string webhookUrl,
            string payload,
            string queueName,
            CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient();
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(webhookUrl, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "Queue '{Q}': Teams webhook returned {Code}. Body: {Body}",
                    queueName, (int)response.StatusCode, body);
            }
        }


        // ── Alert record persistence ───────────────────────────────────────────

        private async Task UpdateAlertRecordAsync(
            QueueConfigEntity config,
            QueueStatusEntity status,
            AlertType alertType,
            AlertRecordEntity? openAlert,
            CancellationToken ct)
        {
            var now = DateTime.UtcNow;

            switch (alertType)
            {
                case AlertType.NewIncident:
                {
                    var id    = Guid.NewGuid().ToString();
                    var alert = new AlertRecordEntity
                    {
                        PartitionKey     = config.QueueName,
                        RowKey           = id,
                        IncidentId       = id,
                        OpenedAtUtc      = now,
                        Status           = "Open",
                        PeakSeverity     = status.AlertSeverity,
                        FirstRootCause   = status.RootCause,
                        LastAlertSentUtc = now,
                        AlertCount       = 1,
                    };
                    await _repository.SaveAlertRecordAsync(alert, ct);
                    break;
                }

                case AlertType.Escalation:
                    openAlert!.PeakSeverity     = "Critical";
                    openAlert.LastAlertSentUtc  = now;
                    openAlert.AlertCount++;
                    await _repository.SaveAlertRecordAsync(openAlert, ct);
                    break;

                case AlertType.Reminder:
                    openAlert!.LastAlertSentUtc = now;
                    openAlert.AlertCount++;
                    await _repository.SaveAlertRecordAsync(openAlert, ct);
                    break;

                case AlertType.Recovery:
                    openAlert!.Status           = "Resolved";
                    openAlert.ResolvedAtUtc     = now;
                    openAlert.LastAlertSentUtc  = now;
                    openAlert.AlertCount++;
                    await _repository.SaveAlertRecordAsync(openAlert, ct);
                    break;
            }
        }
    }
}

using System.Net.Mail;
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

        // SMTP settings from environment (optional — email only sent when all are set)
        private static string? SmtpHost     => Environment.GetEnvironmentVariable("SmtpHost");
        private static string? SmtpUser     => Environment.GetEnvironmentVariable("SmtpUser");
        private static string? SmtpPassword => Environment.GetEnvironmentVariable("SmtpPassword");
        private static string? SmtpFrom     => Environment.GetEnvironmentVariable("SmtpFromAddress");
        private static int     SmtpPort     =>
            int.TryParse(Environment.GetEnvironmentVariable("SmtpPort"), out var p) ? p : 587;

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

            var openAlert = await _repository.GetOpenAlertAsync(config.QueueName, ct);
            var alertType = DetermineAlertType(status, openAlert);

            var teamsTask = SendTeamsAlertAsync(config, status, alertType, openAlert, ct);
            var emailTask = SendEmailAlertAsync(config, status, alertType, openAlert, ct);

            await Task.WhenAll(teamsTask, emailTask);
            await UpdateAlertRecordAsync(config, status, alertType, openAlert, ct);

            _logger.LogInformation(
                "Queue '{Q}': Alert dispatched — type={T} severity={S}.",
                config.QueueName, alertType, status.AlertSeverity);
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


        // ── Teams alert ────────────────────────────────────────────────────────

        private async Task SendTeamsAlertAsync(
            QueueConfigEntity config,
            QueueStatusEntity status,
            AlertType alertType,
            AlertRecordEntity? openAlert,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(config.TeamsWebhookUrl))
            {
                _logger.LogWarning(
                    "Queue '{Q}': TeamsWebhookUrl not configured. Skipping Teams alert.",
                    config.QueueName);
                return;
            }

            try
            {
                // Old Incoming Webhook connector uses webhook.office.com and needs MessageCard format.
                // New Workflows webhook uses logic.azure.com and needs Adaptive Card format.
                string payload = config.TeamsWebhookUrl.Contains("webhook.office.com", StringComparison.OrdinalIgnoreCase)
                    ? BuildLegacyMessageCard(config, status, alertType, openAlert)
                    : BuildAdaptiveCard(config, status, alertType, openAlert);

                await PostToTeamsAsync(config.TeamsWebhookUrl, payload, config.QueueName, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Queue '{Q}': Teams alert failed.",
                    config.QueueName);
            }
        }

        // New Teams Workflows webhook format (Adaptive Card)
        private static string BuildAdaptiveCard(
            QueueConfigEntity config,
            QueueStatusEntity status,
            AlertType alertType,
            AlertRecordEntity? openAlert)
        {
            string title = AlertTitle(config.QueueName, status.AlertSeverity, alertType);

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

            return new JsonObject
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
            }.ToJsonString();
        }

        // Old Incoming Webhook connector format (MessageCard) — deprecated but still in use
        private static string BuildLegacyMessageCard(
            QueueConfigEntity config,
            QueueStatusEntity status,
            AlertType alertType,
            AlertRecordEntity? openAlert)
        {
            string title = AlertTitle(config.QueueName, status.AlertSeverity, alertType);
            string color = status.AlertSeverity == "Critical" ? "FF0000"
                         : status.AlertSeverity == "Warning"  ? "FFA500"
                         : "00B050";

            string text;
            if (alertType == AlertType.Recovery)
            {
                string duration = openAlert != null
                    ? $"{(int)(DateTime.UtcNow - openAlert.OpenedAtUtc).TotalMinutes} minutes"
                    : "unknown";
                text = $"Queue has recovered. Incident duration: {duration}.";
            }
            else
            {
                string waitDisplay = status.WaitTimeMinutes.HasValue
                    ? $"{status.WaitTimeMinutes:F1} min (SLA: {status.SlaMinutes} min)"
                    : $"∞ — consumer stopped (SLA: {status.SlaMinutes} min)";

                text = $"**Root Cause:** {status.RootCause}  \n" +
                       $"**Active Messages:** {status.ActiveCount}  \n" +
                       $"**Wait Time:** {waitDisplay}  \n" +
                       $"**Trend:** {status.TrendLabel}";
            }

            return new JsonObject
            {
                ["@type"]      = "MessageCard",
                ["@context"]   = "http://schema.org/extensions",
                ["themeColor"] = color,
                ["summary"]    = title,
                ["sections"]   = new JsonArray
                {
                    new JsonObject
                    {
                        ["activityTitle"] = title,
                        ["text"]          = text,
                    }
                }
            }.ToJsonString();
        }

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


        // ── Email alert ────────────────────────────────────────────────────────

        private async Task SendEmailAlertAsync(
            QueueConfigEntity config,
            QueueStatusEntity status,
            AlertType alertType,
            AlertRecordEntity? openAlert,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(config.EmailRecipients)) return;

            if (string.IsNullOrWhiteSpace(SmtpHost) ||
                string.IsNullOrWhiteSpace(SmtpUser) ||
                string.IsNullOrWhiteSpace(SmtpPassword))
            {
                _logger.LogWarning(
                    "Queue '{Q}': EmailRecipients is set but SmtpHost/SmtpUser/SmtpPassword are not configured. Skipping email alert.",
                    config.QueueName);
                return;
            }

            try
            {
                string subject = AlertTitle(config.QueueName, status.AlertSeverity, alertType);
                string html    = BuildEmailHtml(config, status, alertType, openAlert);

                using var mail = new MailMessage();
                mail.From       = new MailAddress(SmtpFrom ?? SmtpUser, "QBIS Monitor");
                mail.Subject    = subject;
                mail.Body       = html;
                mail.IsBodyHtml = true;

                foreach (var recipient in config.EmailRecipients.Split(
                    ',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    mail.To.Add(recipient);
                }

                using var smtp = new SmtpClient(SmtpHost, SmtpPort)
                {
                    Credentials = new System.Net.NetworkCredential(SmtpUser, SmtpPassword),
                    EnableSsl   = true,
                };

                await smtp.SendMailAsync(mail, ct);

                _logger.LogInformation(
                    "Queue '{Q}': Email alert sent to {Recipients}.",
                    config.QueueName, config.EmailRecipients);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Queue '{Q}': Email alert failed.",
                    config.QueueName);
            }
        }

        private static string BuildEmailHtml(
            QueueConfigEntity config,
            QueueStatusEntity status,
            AlertType alertType,
            AlertRecordEntity? openAlert)
        {
            string title     = AlertTitle(config.QueueName, status.AlertSeverity, alertType);
            string headerBg  = status.AlertSeverity == "Critical" ? "#dc2626"
                             : status.AlertSeverity == "Warning"  ? "#d97706"
                             : "#16a34a";

            string body;
            if (alertType == AlertType.Recovery)
            {
                string duration = openAlert != null
                    ? $"{(int)(DateTime.UtcNow - openAlert.OpenedAtUtc).TotalMinutes} minutes"
                    : "unknown";
                body = $"<p>Queue <strong>{config.QueueName}</strong> has recovered. Incident duration: {duration}.</p>";
            }
            else
            {
                string waitDisplay = status.WaitTimeMinutes.HasValue
                    ? $"{status.WaitTimeMinutes:F1} min (SLA: {config.SlaMinutes} min)"
                    : $"∞ — consumer stopped (SLA: {config.SlaMinutes} min)";

                body = $@"
                <table style='border-collapse:collapse;width:100%;max-width:500px'>
                  <tr><td style='padding:8px;background:#f3f4f6;font-weight:600'>Root Cause</td>
                      <td style='padding:8px'>{status.RootCause}</td></tr>
                  <tr><td style='padding:8px;background:#f3f4f6;font-weight:600'>Active Messages</td>
                      <td style='padding:8px'>{status.ActiveCount}</td></tr>
                  <tr><td style='padding:8px;background:#f3f4f6;font-weight:600'>Wait Time</td>
                      <td style='padding:8px'>{waitDisplay}</td></tr>
                  <tr><td style='padding:8px;background:#f3f4f6;font-weight:600'>Trend</td>
                      <td style='padding:8px'>{status.TrendLabel}</td></tr>
                </table>";
            }

            return $@"<!DOCTYPE html>
<html>
<body style='font-family:Arial,sans-serif;margin:0;padding:20px;background:#f9fafb'>
  <div style='max-width:600px;margin:auto;background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 1px 3px rgba(0,0,0,0.1)'>
    <div style='background:{headerBg};padding:20px'>
      <h2 style='color:#fff;margin:0;font-size:18px'>{title}</h2>
    </div>
    <div style='padding:20px'>
      {body}
      <hr style='margin:20px 0;border:none;border-top:1px solid #e5e7eb'>
      <p style='color:#6b7280;font-size:12px;margin:0'>Queue Backlog Intelligence System · {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC</p>
    </div>
  </div>
</body>
</html>";
        }


        // ── Shared helpers ─────────────────────────────────────────────────────

        private static string AlertTitle(string queueName, string severity, AlertType alertType) =>
            alertType switch
            {
                AlertType.NewIncident when severity == "Critical" => $"🔴 CRITICAL — {queueName}",
                AlertType.NewIncident                             => $"🟡 WARNING — {queueName}",
                AlertType.Escalation                             => $"🔴 ESCALATED TO CRITICAL — {queueName}",
                AlertType.Reminder   when severity == "Critical" => $"🔴 STILL CRITICAL — {queueName} (reminder)",
                AlertType.Reminder                               => $"🟡 STILL WARNING — {queueName} (reminder)",
                AlertType.Recovery                               => $"✅ RESOLVED — {queueName}",
                _                                                => queueName,
            };


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

using System.Text;
using System.Text.Json;
using Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Notifications;

public class WebhookNotificationChannel : INotificationChannel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<WebhookNotificationChannel> _logger;

    public string ChannelType => "webhook";

    public WebhookNotificationChannel(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<WebhookNotificationChannel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(AlertEvent alert, CancellationToken ct = default)
    {
        var url = _config["Notifications:Webhook:Url"];
        if (string.IsNullOrEmpty(url))
        {
            _logger.LogWarning("Webhook notification channel is not configured — skipping.");
            return;
        }

        var payload = JsonSerializer.Serialize(new
        {
            ruleId = alert.RuleId,
            adapterId = alert.AdapterId,
            tagAddress = alert.TagAddress,
            triggeredValue = alert.TriggeredValue,
            condition = alert.Condition.ToString(),
            threshold = alert.Threshold,
            triggeredAt = alert.TriggeredAt,
            message = alert.Message
        });

        using var client = _httpClientFactory.CreateClient();
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();
    }
}

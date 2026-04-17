using System.Text;
using System.Text.Json;
using Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Notifications;

public class SlackNotificationChannel : INotificationChannel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<SlackNotificationChannel> _logger;

    public string ChannelType => "slack";

    public SlackNotificationChannel(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<SlackNotificationChannel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(AlertEvent alert, CancellationToken ct = default)
    {
        var webhookUrl = _config["Notifications:Slack:WebhookUrl"];
        if (string.IsNullOrEmpty(webhookUrl))
        {
            _logger.LogWarning("Slack notification channel is not configured — skipping.");
            return;
        }

        var payload = JsonSerializer.Serialize(new { text = alert.Message });
        using var client = _httpClientFactory.CreateClient();
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(webhookUrl, content, ct);
        response.EnsureSuccessStatusCode();
    }
}

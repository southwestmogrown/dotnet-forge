using System.Collections.Concurrent;
using Core.Interfaces;

namespace Api.IntegrationTests;

/// <summary>
/// A test notification channel that captures all dispatched alerts
/// without hitting real SMTP/Slack/webhook endpoints.
/// </summary>
public class MockNotificationChannel : INotificationChannel
{
    public string ChannelType { get; }

    /// <summary>
    /// Thread-safe collection of all alerts sent through this channel.
    /// </summary>
    public ConcurrentBag<AlertEvent> SentAlerts { get; } = new();

    public MockNotificationChannel(string channelType = "mock")
    {
        ChannelType = channelType;
    }

    public Task SendAsync(AlertEvent alert, CancellationToken ct = default)
    {
        SentAlerts.Add(alert);
        return Task.CompletedTask;
    }
}

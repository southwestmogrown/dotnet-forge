using Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class NotificationDispatcher
{
    private readonly IEnumerable<INotificationChannel> _channels;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        IEnumerable<INotificationChannel> channels,
        ILogger<NotificationDispatcher> logger)
    {
        _channels = channels;
        _logger = logger;
    }

    public async Task DispatchAsync(
        AlertEvent alert,
        IEnumerable<string> channelTypes,
        CancellationToken ct = default)
    {
        var targets = _channels
            .Where(c => channelTypes.Contains(c.ChannelType))
            .ToList();

        await Task.WhenAll(targets.Select(async channel =>
        {
            try { await channel.SendAsync(alert, ct); }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Notification failed on channel {Channel}", channel.ChannelType);
            }
        }));
    }
}

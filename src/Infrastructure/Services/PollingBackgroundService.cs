using Core.Entities;
using Core.Interfaces;
using Infrastructure.Adapters;
using Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class PollingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<DeviceDataHub> _hub;
    private readonly AdapterFactory _adapterFactory;
    private readonly ILogger<PollingBackgroundService> _logger;
    private readonly TimeSpan _pollInterval;

    public PollingBackgroundService(
        IServiceScopeFactory scopeFactory,
        IHubContext<DeviceDataHub> hub,
        AdapterFactory adapterFactory,
        ILogger<PollingBackgroundService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _hub = hub;
        _adapterFactory = adapterFactory;
        _logger = logger;

        var intervalSeconds = configuration.GetValue<double>("Polling:IntervalSeconds", 1.0);
        _pollInterval = TimeSpan.FromSeconds(intervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var adapter in _adapterFactory.GetAll())
            {
                if (!adapter.IsConnected) continue;

                var tags = GetTagsForAdapter(adapter.AdapterId);

                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider
                    .GetRequiredService<IRepository<SensorReading>>();

                var alertEvaluator = scope.ServiceProvider
                    .GetRequiredService<AlertEvaluator>();

                foreach (var tag in tags)
                {
                    try
                    {
                        var reading = await adapter.ReadTagAsync(tag, stoppingToken);

                        await repo.AddAsync(new SensorReading
                        {
                            AdapterId = reading.AdapterId,
                            TagAddress = reading.TagAddress,
                            Value = reading.Value?.ToString() ?? string.Empty,
                            Unit = reading.Unit,
                            RecordedAt = reading.Timestamp
                        }, stoppingToken);

                        await _hub.Clients
                            .Group(DeviceDataHub.GroupKey(reading.AdapterId, tag))
                            .SendAsync("TagUpdate", reading, stoppingToken);

                        try
                        {
                            await alertEvaluator.EvaluateAsync(reading, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex,
                                "Alert evaluation failed for tag {Tag} on adapter {Adapter}",
                                tag, adapter.AdapterId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Error polling tag {Tag} on adapter {Adapter}", tag, adapter.AdapterId);
                    }
                }
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    private IEnumerable<string> GetTagsForAdapter(string adapterId)
    {
        var config = _adapterFactory.GetConfig(adapterId);
        if (config?.Options is null || !config.Options.TryGetValue("tags", out var tags))
            return [];

        return tags
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

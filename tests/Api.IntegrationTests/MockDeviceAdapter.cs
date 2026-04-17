using Core.Interfaces;

namespace Api.IntegrationTests;

/// <summary>
/// A mock IDeviceAdapter that returns deterministic fake readings
/// without needing real hardware (Modbus/OPC-UA).
/// </summary>
public class MockDeviceAdapter : IDeviceAdapter
{
    private AdapterConfig? _config;

    public string AdapterId { get; private set; } = string.Empty;
    public bool IsConnected { get; private set; }

    public Task ConnectAsync(AdapterConfig config, CancellationToken ct = default)
    {
        _config = config;
        AdapterId = $"mock-{config.Host}:{config.Port}";
        IsConnected = true;
        return Task.CompletedTask;
    }

    public Task<TagValue> ReadTagAsync(string tagAddress, CancellationToken ct = default)
    {
        return Task.FromResult(new TagValue(
            AdapterId,
            tagAddress,
            42.0,
            DateTime.UtcNow,
            "units"));
    }

    public Task WriteTagAsync(string tagAddress, object value, CancellationToken ct = default)
        => Task.CompletedTask;

    public async IAsyncEnumerable<TagValue> SubscribeAsync(
        string tagAddress,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            yield return await ReadTagAsync(tagAddress, ct);
            await Task.Delay(_config?.PollInterval ?? TimeSpan.FromSeconds(1), ct);
        }
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        IsConnected = false;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        IsConnected = false;
        return ValueTask.CompletedTask;
    }
}

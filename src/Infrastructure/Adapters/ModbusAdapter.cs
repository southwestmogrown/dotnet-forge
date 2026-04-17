using Core.Interfaces;

namespace Infrastructure.Adapters;

public class ModbusAdapter : IDeviceAdapter
{
    public string AdapterId { get; private set; } = string.Empty;
    public bool IsConnected => false;

    public Task ConnectAsync(AdapterConfig config, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<TagValue> ReadTagAsync(string tagAddress, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task WriteTagAsync(string tagAddress, object value, CancellationToken ct = default)
        => throw new NotImplementedException();

    public IAsyncEnumerable<TagValue> SubscribeAsync(string tagAddress, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task DisconnectAsync(CancellationToken ct = default)
        => throw new NotImplementedException();

    public ValueTask DisposeAsync()
        => throw new NotImplementedException();
}

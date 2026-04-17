using System.Collections.Concurrent;
using Core.Interfaces;

namespace Infrastructure.Adapters;

public class AdapterFactory
{
    private readonly ConcurrentDictionary<string, IDeviceAdapter> _adapters = new();

    public async Task<IDeviceAdapter> RegisterAsync(AdapterConfig config, CancellationToken ct = default)
    {
        IDeviceAdapter adapter = config.Protocol.ToLower() switch
        {
            "modbus" => new ModbusAdapter(),
            "opcua"  => new OpcUaAdapter(),
            _ => throw new ArgumentException($"Unknown protocol: {config.Protocol}")
        };

        await adapter.ConnectAsync(config, ct);

        if (_adapters.TryRemove(adapter.AdapterId, out var existing))
        {
            await existing.DisconnectAsync(ct);
            await existing.DisposeAsync();
        }

        _adapters[adapter.AdapterId] = adapter;
        return adapter;
    }

    public IEnumerable<IDeviceAdapter> GetAll() => _adapters.Values;

    public IDeviceAdapter? Get(string adapterId) =>
        _adapters.TryGetValue(adapterId, out var adapter) ? adapter : null;

    public async Task RemoveAsync(string adapterId, CancellationToken ct = default)
    {
        if (_adapters.TryRemove(adapterId, out var adapter))
        {
            await adapter.DisconnectAsync(ct);
            await adapter.DisposeAsync();
        }
    }
}

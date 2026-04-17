using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Core.Interfaces;
using NModbus;

namespace Infrastructure.Adapters;

public class ModbusAdapter : IDeviceAdapter
{
    private IModbusMaster? _master;
    private TcpClient? _client;
    private AdapterConfig? _config;

    public string AdapterId { get; private set; } = string.Empty;
    public bool IsConnected => _client?.Connected ?? false;

    public async Task ConnectAsync(AdapterConfig config, CancellationToken ct = default)
    {
        _config = config;
        AdapterId = $"modbus-{config.Host}:{config.Port}";
        _client = new TcpClient();
        await _client.ConnectAsync(config.Host, config.Port, ct);
        var factory = new ModbusFactory();
        _master = factory.CreateMaster(_client);
    }

    public async Task<TagValue> ReadTagAsync(string tagAddress, CancellationToken ct = default)
    {
        // tagAddress format: "HR:0:1" = register type, start address, count
        var parts = tagAddress.Split(':');
        var type = parts[0];    // HR, CO, DI, IR
        var start = ushort.Parse(parts[1]);
        var count = ushort.Parse(parts.Length > 2 ? parts[2] : "1");

        object value = type switch
        {
            "HR" => await _master!.ReadHoldingRegistersAsync(1, start, count),
            "CO" => await _master!.ReadCoilsAsync(1, start, count),
            "DI" => await _master!.ReadInputsAsync(1, start, count),
            "IR" => await _master!.ReadInputRegistersAsync(1, start, count),
            _ => throw new ArgumentException($"Unknown register type: {type}")
        };

        return new TagValue(AdapterId, tagAddress, value, DateTime.UtcNow);
    }

    public async Task WriteTagAsync(string tagAddress, object value, CancellationToken ct = default)
    {
        var parts = tagAddress.Split(':');
        var type = parts[0];
        var address = ushort.Parse(parts[1]);

        if (type == "HR")
            await _master!.WriteSingleRegisterAsync(1, address, Convert.ToUInt16(value));
        else if (type == "CO")
            await _master!.WriteSingleCoilAsync(1, address, Convert.ToBoolean(value));
        else
            throw new NotSupportedException($"Write not supported for register type: {type}");
    }

    public async IAsyncEnumerable<TagValue> SubscribeAsync(
        string tagAddress,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            yield return await ReadTagAsync(tagAddress, ct);
            await Task.Delay(_config!.PollInterval, ct);
        }
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        _master?.Dispose();
        _client?.Close();
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _master?.Dispose();
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }
}

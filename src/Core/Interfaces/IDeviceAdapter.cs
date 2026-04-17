namespace Core.Interfaces;

public interface IDeviceAdapter : IAsyncDisposable
{
    string AdapterId { get; }
    bool IsConnected { get; }

    Task ConnectAsync(AdapterConfig config, CancellationToken ct = default);
    Task<TagValue> ReadTagAsync(string tagAddress, CancellationToken ct = default);
    Task WriteTagAsync(string tagAddress, object value, CancellationToken ct = default);
    IAsyncEnumerable<TagValue> SubscribeAsync(string tagAddress, CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
}

public record AdapterConfig(
    string Host,
    int Port,
    string Protocol,            // "opcua" | "modbus"
    TimeSpan PollInterval,
    Dictionary<string, string>? Options = null);

public record TagValue(
    string AdapterId,
    string TagAddress,
    object Value,
    DateTime Timestamp,     // UTC
    string Unit = "");

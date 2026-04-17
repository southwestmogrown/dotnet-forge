using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

[Authorize]
public class DeviceDataHub : Hub
{
    public async Task SubscribeToTag(string adapterId, string tagAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adapterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tagAddress);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupKey(adapterId, tagAddress));
    }

    public async Task UnsubscribeFromTag(string adapterId, string tagAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adapterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tagAddress);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupKey(adapterId, tagAddress));
    }

    private static string GroupKey(string adapterId, string tagAddress) =>
        $"{adapterId}::{tagAddress}";
}

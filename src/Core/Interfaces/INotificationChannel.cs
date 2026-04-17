using Core.Entities;

namespace Core.Interfaces;

public interface INotificationChannel
{
    string ChannelType { get; }
    Task SendAsync(AlertEvent alert, CancellationToken ct = default);
}

public class AlertEvent
{
    public Guid RuleId { get; set; }
    public string AdapterId { get; set; } = string.Empty;
    public string TagAddress { get; set; } = string.Empty;
    public object TriggeredValue { get; set; } = new();
    public AlertCondition Condition { get; set; }
    public double Threshold { get; set; }
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;

    public string Message =>
        $"[ALERT] {TagAddress} is {TriggeredValue} " +
        $"(rule: {Condition} {Threshold}) @ {TriggeredAt:u}";
}

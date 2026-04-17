namespace Core.Entities;

public class AlertRule : BaseEntity
{
    public string AdapterId { get; set; } = string.Empty;
    public string TagAddress { get; set; } = string.Empty;
    public AlertCondition Condition { get; set; }
    public double Threshold { get; set; }
    public TimeSpan Cooldown { get; set; } = TimeSpan.FromMinutes(5);
    public bool IsEnabled { get; set; } = true;
    public string NotificationChannels { get; set; } = "[]";  // JSON: ["email","slack"]
    public DateTime? LastTriggeredAt { get; set; }
}

public enum AlertCondition { GreaterThan, LessThan, Equal, NotEqual }

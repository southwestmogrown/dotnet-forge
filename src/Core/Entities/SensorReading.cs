namespace Core.Entities;

public class SensorReading : BaseEntity
{
    public string AdapterId { get; set; } = string.Empty;
    public string TagAddress { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}

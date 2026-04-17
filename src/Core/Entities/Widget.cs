using System.ComponentModel.DataAnnotations;

namespace Core.Entities;

public class Widget : BaseEntity
{
    [MaxLength(200)] public string Name { get; set; } = string.Empty;
    [MaxLength(2000)] public string Description { get; set; } = string.Empty;
}

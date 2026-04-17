using Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data;

public class SensorReadingConfiguration : IEntityTypeConfiguration<SensorReading>
{
    public void Configure(EntityTypeBuilder<SensorReading> builder)
    {
        builder.HasIndex(r => r.RecordedAt);
    }
}

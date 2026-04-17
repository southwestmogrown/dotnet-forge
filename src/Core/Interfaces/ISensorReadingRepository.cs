using Core.Entities;

namespace Core.Interfaces;

public interface ISensorReadingRepository : IRepository<SensorReading>
{
    Task<IEnumerable<SensorReading>> GetByAdapterAsync(string adapterId, CancellationToken ct = default);
    Task<IEnumerable<SensorReading>> GetByTagAsync(string adapterId, string tagAddress, DateTime from, DateTime to, CancellationToken ct = default);
    Task<SensorReading?> GetLatestAsync(string adapterId, string tagAddress, CancellationToken ct = default);
}

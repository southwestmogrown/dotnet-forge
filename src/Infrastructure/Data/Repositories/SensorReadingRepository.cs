using Core.Entities;
using Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data.Repositories;

public class SensorReadingRepository : GenericRepository<SensorReading>, ISensorReadingRepository
{
    public SensorReadingRepository(AppDbContext ctx) : base(ctx) { }

    public async Task<IEnumerable<SensorReading>> GetByAdapterAsync(
        string adapterId, CancellationToken ct = default) =>
        await _set.AsNoTracking()
            .Where(r => r.AdapterId == adapterId)
            .OrderBy(r => r.RecordedAt)
            .ToListAsync(ct);

    public async Task<IEnumerable<SensorReading>> GetByTagAsync(
        string adapterId, string tagAddress, DateTime from, DateTime to,
        CancellationToken ct = default) =>
        await _set.AsNoTracking()
            .Where(r => r.AdapterId == adapterId
                     && r.TagAddress == tagAddress
                     && r.RecordedAt >= from
                     && r.RecordedAt <= to)
            .OrderBy(r => r.RecordedAt)
            .ToListAsync(ct);

    public async Task<SensorReading?> GetLatestAsync(
        string adapterId, string tagAddress, CancellationToken ct = default) =>
        await _set.AsNoTracking()
            .Where(r => r.AdapterId == adapterId && r.TagAddress == tagAddress)
            .OrderByDescending(r => r.RecordedAt)
            .FirstOrDefaultAsync(ct);
}

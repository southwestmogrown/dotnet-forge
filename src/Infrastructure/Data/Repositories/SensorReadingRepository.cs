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

    public async Task<IEnumerable<SensorReading>> GetFilteredAsync(
        string adapterId, string? tagAddress, DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = _set.AsNoTracking().Where(r => r.AdapterId == adapterId);

        if (!string.IsNullOrEmpty(tagAddress))
            query = query.Where(r => r.TagAddress == tagAddress);
        if (from.HasValue)
            query = query.Where(r => r.RecordedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(r => r.RecordedAt <= to.Value);

        return await query
            .OrderBy(r => r.RecordedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<SensorReading>> GetLatestPerTagAsync(
        string adapterId, CancellationToken ct = default)
    {
        var latestTimes = _set.AsNoTracking()
            .Where(r => r.AdapterId == adapterId)
            .GroupBy(r => r.TagAddress)
            .Select(g => new { TagAddress = g.Key, MaxRecordedAt = g.Max(r => r.RecordedAt) });

        return await _set.AsNoTracking()
            .Where(r => r.AdapterId == adapterId)
            .Join(latestTimes,
                r => new { r.TagAddress, r.RecordedAt },
                lt => new { lt.TagAddress, RecordedAt = lt.MaxRecordedAt },
                (r, _) => r)
            .ToListAsync(ct);
    }
}

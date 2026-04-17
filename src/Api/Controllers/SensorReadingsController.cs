using Core.Entities;
using Core.Interfaces;
using Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Authorize]
public class SensorReadingsController : BaseApiController
{
    private readonly ISensorReadingRepository _repo;

    public SensorReadingsController(ISensorReadingRepository repo)
    {
        _repo = repo;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<SensorReadingDto>>>> GetReadings(
        [FromQuery] string adapterId,
        [FromQuery] string? tagAddress,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var readings = await _repo.GetFilteredAsync(
            adapterId, tagAddress, from, to, page, pageSize, cancellationToken);

        return OkResult(readings.Select(ToDto));
    }

    [HttpGet("latest")]
    public async Task<ActionResult<ApiResponse<IEnumerable<SensorReadingDto>>>> GetLatest(
        [FromQuery] string adapterId,
        CancellationToken cancellationToken = default)
    {
        var readings = await _repo.GetLatestPerTagAsync(adapterId, cancellationToken);
        return OkResult(readings.Select(ToDto));
    }

    private static SensorReadingDto ToDto(SensorReading r) =>
        new(r.Id, r.AdapterId, r.TagAddress, r.Value, r.Unit, r.RecordedAt);
}

public record SensorReadingDto(
    Guid Id,
    string AdapterId,
    string TagAddress,
    string Value,
    string Unit,
    DateTime RecordedAt);

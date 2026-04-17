using Core.Interfaces;
using Core.Models;
using Infrastructure.Adapters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Authorize]
public class AdaptersController : BaseApiController
{
    private readonly AdapterFactory _adapterFactory;

    public AdaptersController(AdapterFactory adapterFactory)
    {
        _adapterFactory = adapterFactory;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<AdapterDto>>> Register(
        [FromBody] RegisterAdapterRequest request, CancellationToken cancellationToken)
    {
        var options = request.Tags is { Length: > 0 }
            ? new Dictionary<string, string> { ["tags"] = string.Join(",", request.Tags) }
            : null;

        var config = new AdapterConfig(
            request.Host,
            request.Port,
            request.Protocol,
            TimeSpan.FromSeconds(request.PollIntervalSeconds),
            options);

        var adapter = await _adapterFactory.RegisterAsync(config, cancellationToken);

        return OkResult(new AdapterDto(
            adapter.AdapterId,
            config.Protocol,
            config.Host,
            config.Port,
            adapter.IsConnected));
    }

    [HttpGet]
    public ActionResult<ApiResponse<IEnumerable<AdapterDto>>> GetAll()
    {
        var dtos = _adapterFactory.GetAll().Select(a =>
        {
            var config = _adapterFactory.GetConfig(a.AdapterId);
            return new AdapterDto(
                a.AdapterId,
                config?.Protocol ?? string.Empty,
                config?.Host ?? string.Empty,
                config?.Port ?? 0,
                a.IsConnected);
        });

        return OkResult(dtos);
    }

    [HttpDelete("{adapterId}")]
    public async Task<IActionResult> Remove(string adapterId, CancellationToken cancellationToken)
    {
        await _adapterFactory.RemoveAsync(adapterId, cancellationToken);
        return NoContent();
    }
}

public record RegisterAdapterRequest(
    string Host,
    int Port,
    string Protocol,
    int PollIntervalSeconds,
    string[]? Tags = null);

public record AdapterDto(
    string AdapterId,
    string Protocol,
    string Host,
    int Port,
    bool IsConnected);

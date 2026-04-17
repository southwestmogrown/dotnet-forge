using Core.Entities;
using Core.Exceptions;
using Core.Interfaces;
using Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Authorize]
public class AlertRulesController : BaseApiController
{
    private readonly IRepository<AlertRule> _repo;

    public AlertRulesController(IRepository<AlertRule> repo)
    {
        _repo = repo;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<AlertRule>>>> GetAll(
        CancellationToken cancellationToken)
    {
        var rules = await _repo.GetAllAsync(cancellationToken);
        return OkResult(rules);
    }

    [HttpGet("{id:guid}", Name = "GetAlertRule")]
    public async Task<ActionResult<ApiResponse<AlertRule>>> GetById(
        Guid id, CancellationToken cancellationToken)
    {
        var rule = await _repo.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(AlertRule), id);
        return OkResult(rule);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<AlertRule>>> Create(
        [FromBody] AlertRuleRequest request, CancellationToken cancellationToken)
    {
        var rule = new AlertRule
        {
            AdapterId = request.AdapterId,
            TagAddress = request.TagAddress,
            Condition = request.Condition,
            Threshold = request.Threshold,
            Cooldown = TimeSpan.FromSeconds(request.CooldownSeconds),
            IsEnabled = request.IsEnabled,
            NotificationChannels = System.Text.Json.JsonSerializer.Serialize(request.NotificationChannels)
        };
        var created = await _repo.AddAsync(rule, cancellationToken);
        return CreatedResult("GetAlertRule", new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<AlertRule>>> Update(
        Guid id, [FromBody] AlertRuleRequest request, CancellationToken cancellationToken)
    {
        var rule = await _repo.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(AlertRule), id);

        rule.AdapterId = request.AdapterId;
        rule.TagAddress = request.TagAddress;
        rule.Condition = request.Condition;
        rule.Threshold = request.Threshold;
        rule.Cooldown = TimeSpan.FromSeconds(request.CooldownSeconds);
        rule.IsEnabled = request.IsEnabled;
        rule.NotificationChannels = System.Text.Json.JsonSerializer.Serialize(request.NotificationChannels);
        await _repo.UpdateAsync(rule, cancellationToken);
        return OkResult(rule);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(
        Guid id, CancellationToken cancellationToken)
    {
        await _repo.DeleteAsync(id, cancellationToken);
        return OkResult(true);
    }
}

public record AlertRuleRequest(
    string AdapterId,
    string TagAddress,
    AlertCondition Condition,
    double Threshold,
    double CooldownSeconds = 300,
    bool IsEnabled = true,
    string[] NotificationChannels = default!);

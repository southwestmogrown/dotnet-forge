using Core.Entities;
using Core.Exceptions;
using Core.Interfaces;
using Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Authorize]
public class WidgetsController : BaseApiController
{
    private readonly IRepository<Widget> _repo;

    public WidgetsController(IRepository<Widget> repo)
    {
        _repo = repo;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<Widget>>>> GetAll(
        CancellationToken cancellationToken)
    {
        var widgets = await _repo.GetAllAsync(cancellationToken);
        return OkResult(widgets);
    }

    [HttpGet("{id:guid}", Name = "GetWidget")]
    public async Task<ActionResult<ApiResponse<Widget>>> GetById(
        Guid id, CancellationToken cancellationToken)
    {
        var widget = await _repo.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Widget), id);
        return OkResult(widget);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Widget>>> Create(
        [FromBody] WidgetRequest request, CancellationToken cancellationToken)
    {
        var widget = new Widget { Name = request.Name, Description = request.Description };
        var created = await _repo.AddAsync(widget, cancellationToken);
        return CreatedResult("GetWidget", new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<Widget>>> Update(
        Guid id, [FromBody] WidgetRequest request, CancellationToken cancellationToken)
    {
        var widget = await _repo.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Widget), id);

        widget.Name = request.Name;
        widget.Description = request.Description;
        await _repo.UpdateAsync(widget, cancellationToken);
        return OkResult(widget);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(
        Guid id, CancellationToken cancellationToken)
    {
        await _repo.DeleteAsync(id, cancellationToken);
        return OkResult(true);
    }
}

public record WidgetRequest(string Name, string Description);

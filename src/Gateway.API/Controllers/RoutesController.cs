using Gateway.API.Proxy;
using Gateway.Core.DTOs;
using Gateway.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gateway.API.Controllers;

[ApiController]
[Route("gateway/routes")]
[Authorize(Roles = "admin")]
public class RoutesController : ControllerBase
{
    private readonly RouteService _routeService;
    private readonly RouteChangeNotifier _notifier;

    public RoutesController(RouteService routeService, RouteChangeNotifier notifier)
    {
        _routeService = routeService;
        _notifier = notifier;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] RouteCreateDto dto, CancellationToken ct)
    {
        var result = await _routeService.CreateAsync(dto, ct);
        _notifier.NotifyChange();
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    [Authorize(Roles = "admin,viewer")]
    public async Task<IActionResult> GetAll([FromQuery] bool? isActive, CancellationToken ct)
    {
        var results = await _routeService.GetAllAsync(isActive, ct);
        return Ok(results);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _routeService.GetByIdAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] RouteUpdateDto dto, CancellationToken ct)
    {
        var result = await _routeService.UpdateAsync(id, dto, ct);
        if (result is not null) _notifier.NotifyChange();
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await _routeService.DeleteAsync(id, ct);
        if (deleted) _notifier.NotifyChange();
        return deleted ? NoContent() : NotFound();
    }
}

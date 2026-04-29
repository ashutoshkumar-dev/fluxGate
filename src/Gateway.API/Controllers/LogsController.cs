using Gateway.Core.DTOs;
using Gateway.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Gateway.API.Controllers;

[ApiController]
public class LogsController : ControllerBase
{
    private readonly ISeqLogsService _logs;

    public LogsController(ISeqLogsService logs) => _logs = logs;

    /// <summary>
    /// GET /logs — returns paginated log entries from Seq.
    ///
    /// Query params:
    ///   route    — filter by RequestPath (partial match)
    ///   status   — filter by level: error | warning | information
    ///   from     — ISO-8601 start date
    ///   to       — ISO-8601 end date
    ///   page     — 1-based page number (default 1)
    ///   pageSize — items per page (default 50, max 200)
    /// </summary>
    [HttpGet("/logs")]
    public async Task<ActionResult<LogPageDto>> GetLogs(
        [FromQuery] string?  route    = null,
        [FromQuery] string?  status   = null,
        [FromQuery] string?  from     = null,
        [FromQuery] string?  to       = null,
        [FromQuery] int      page     = 1,
        [FromQuery] int      pageSize = 50,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page     = Math.Max(1, page);

        var query = new LogQueryDto
        {
            Route    = route,
            Status   = status,
            From     = from,
            To       = to,
            Page     = page,
            PageSize = pageSize,
        };

        var result = await _logs.QueryLogsAsync(query, ct);
        return Ok(result);
    }
}

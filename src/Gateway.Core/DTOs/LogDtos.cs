namespace Gateway.Core.DTOs;

/// <summary>
/// Query parameters for GET /logs.
/// </summary>
public sealed class LogQueryDto
{
    public string? Route    { get; set; }
    public string? Status   { get; set; }  // "error", "warning", "information" etc.
    public string? From     { get; set; }  // ISO-8601
    public string? To       { get; set; }  // ISO-8601
    public int     Page     { get; set; } = 1;
    public int     PageSize { get; set; } = 50;
}

/// <summary>
/// Paginated log response returned by GET /logs.
/// </summary>
public sealed class LogPageDto
{
    public int                    Page       { get; init; }
    public int                    PageSize   { get; init; }
    public int                    TotalCount { get; init; }
    public IReadOnlyList<LogEntryDto> Items  { get; init; } = [];
}

public sealed class LogEntryDto
{
    public DateTimeOffset Timestamp   { get; init; }
    public string         Level       { get; init; } = "";
    public string         Message     { get; init; } = "";
    public string?        Route       { get; init; }
    public string?        Method      { get; init; }
    public int?           StatusCode  { get; init; }
    public double?        LatencyMs   { get; init; }
    public string?        TraceId     { get; init; }
    public string?        UserId      { get; init; }
    public string?        Exception   { get; init; }
}

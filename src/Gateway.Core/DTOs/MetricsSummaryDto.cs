namespace Gateway.Core.DTOs;

/// <summary>
/// Aggregated metrics summary returned by GET /metrics/summary.
/// </summary>
public sealed class MetricsSummaryDto
{
    public long   TotalRequests   { get; init; }
    public long   TotalErrors     { get; init; }
    public double ErrorRate       { get; init; }  // 0.0 – 1.0
    public double AvgLatencyMs    { get; init; }
    public IReadOnlyList<RouteMetricDto> TopRoutes { get; init; } = [];
}

public sealed class RouteMetricDto
{
    public string Route        { get; init; } = "";
    public long   Requests     { get; init; }
    public long   Errors       { get; init; }
    public double AvgLatencyMs { get; init; }
}

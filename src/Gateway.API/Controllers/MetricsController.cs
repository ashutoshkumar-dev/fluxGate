using Gateway.API.Metrics;
using Gateway.Core.DTOs;
using Microsoft.AspNetCore.Mvc;
using PrometheusMetrics = Prometheus.Metrics;

namespace Gateway.API.Controllers;

[ApiController]
public class MetricsController : ControllerBase
{
    private readonly MetricsRegistry _registry;

    public MetricsController(MetricsRegistry registry) => _registry = registry;

    /// <summary>
    /// GET /metrics — Prometheus text exposition format.
    /// Prometheus will scrape this endpoint.
    /// </summary>
    [HttpGet("/metrics")]
    public async Task GetPrometheus()
    {
        Response.ContentType = "text/plain; version=0.0.4; charset=utf-8";
        await using var ms = new MemoryStream();
        await PrometheusMetrics.DefaultRegistry.CollectAndExportAsTextAsync(ms, HttpContext.RequestAborted);
        ms.Seek(0, SeekOrigin.Begin);
        await ms.CopyToAsync(Response.Body, HttpContext.RequestAborted);
    }

    /// <summary>
    /// GET /metrics/summary — JSON aggregated stats from the in-memory registry.
    /// </summary>
    [HttpGet("/metrics/summary")]
    public ActionResult<MetricsSummaryDto> GetSummary()
    {
        var snapshot = _registry.Snapshot();

        var totalRequests = snapshot.Sum(r => r.Total);
        var totalErrors   = snapshot.Sum(r => r.Errors);
        var avgLatencyMs  = totalRequests == 0
            ? 0
            : snapshot.Sum(r => r.LatencyMs * r.Total) / totalRequests;

        var topRoutes = snapshot
            .OrderByDescending(r => r.Total)
            .Take(10)
            .Select(r => new RouteMetricDto
            {
                Route        = r.Route,
                Requests     = r.Total,
                Errors       = r.Errors,
                AvgLatencyMs = r.LatencyMs,
            })
            .ToList();

        return Ok(new MetricsSummaryDto
        {
            TotalRequests = totalRequests,
            TotalErrors   = totalErrors,
            ErrorRate     = totalRequests == 0 ? 0 : (double)totalErrors / totalRequests,
            AvgLatencyMs  = avgLatencyMs,
            TopRoutes     = topRoutes,
        });
    }
}

using Gateway.API.Metrics;
using System.Diagnostics;
using PrometheusMetrics = Prometheus.Metrics;

namespace Gateway.API.Middleware;

/// <summary>
/// Records Prometheus metrics for every HTTP request:
///   - http_requests_total   counter  (labels: method, route, status_code)
///   - http_request_duration_seconds histogram (labels: method, route)
///
/// Placed early in the pipeline (after UseSerilogRequestLogging, before auth)
/// so it captures every request regardless of auth outcome.
/// </summary>
public sealed class MetricsMiddleware
{
    private static readonly Prometheus.Counter RequestsTotal = PrometheusMetrics.CreateCounter(
        "http_requests_total",
        "Total HTTP requests processed by the Gateway.",
        new Prometheus.CounterConfiguration { LabelNames = ["method", "route", "status_code"] });

    private static readonly Prometheus.Histogram RequestDuration = PrometheusMetrics.CreateHistogram(
        "http_request_duration_seconds",
        "HTTP request latency in seconds.",
        new Prometheus.HistogramConfiguration
        {
            LabelNames = ["method", "route"],
            Buckets = Prometheus.Histogram.ExponentialBuckets(0.005, 2, 12)
        });

    private readonly RequestDelegate  _next;
    private readonly MetricsRegistry  _registry;

    public MetricsMiddleware(RequestDelegate next, MetricsRegistry registry)
    {
        _next     = next;
        _registry = registry;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _next(ctx);
        }
        finally
        {
            sw.Stop();

            // Normalise the route label: prefer matched controller route template,
            // fall back to the request path (truncated to avoid high-cardinality issues).
            var routeLabel = ctx.GetEndpoint()?.DisplayName
                          ?? TrimPath(ctx.Request.Path.Value ?? "/");

            var method = ctx.Request.Method.ToUpperInvariant();
            var status = ctx.Response.StatusCode.ToString();

            RequestsTotal.WithLabels(method, routeLabel, status).Inc();
            RequestDuration.WithLabels(method, routeLabel).Observe(sw.Elapsed.TotalSeconds);
            _registry.Record(method, routeLabel, ctx.Response.StatusCode, sw.Elapsed.TotalSeconds);
        }
    }

    private static string TrimPath(string path)
    {
        // Keep the first two path segments to avoid unbounded label cardinality
        // e.g. /api/orders/12345/items  ->  /api/orders
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "/",
            1 => $"/{parts[0]}",
            _ => $"/{parts[0]}/{parts[1]}"
        };
    }
}

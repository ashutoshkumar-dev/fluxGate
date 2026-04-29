using System.Collections.Concurrent;

namespace Gateway.API.Metrics;

/// <summary>
/// In-memory store of per-route aggregated metrics.
/// Updated by MetricsMiddleware; read by MetricsController for /metrics/summary.
/// Thread-safe via ConcurrentDictionary + Interlocked.
/// </summary>
public sealed class MetricsRegistry
{
    private readonly ConcurrentDictionary<string, RouteStats> _stats = new();

    /// <summary>Record a completed request.</summary>
    public void Record(string method, string route, int statusCode, double latencySeconds)
    {
        var key = $"{method}:{route}";
        var stats = _stats.GetOrAdd(key, _ => new RouteStats(route, method));
        stats.Record(statusCode, latencySeconds);
    }

    /// <summary>Snapshot of all current stats (immutable copy).</summary>
    public IReadOnlyList<RouteStats> Snapshot() => [.. _stats.Values];

    // ── Inner record ──────────────────────────────────────────────────────

    public sealed class RouteStats
    {
        public string Route  { get; }
        public string Method { get; }

        private long   _total;
        private long   _errors;
        private double _latencySum;   // protected by lock(_lock)
        private readonly object _lock = new();

        public RouteStats(string route, string method)
        {
            Route  = route;
            Method = method;
        }

        public void Record(int statusCode, double latencySeconds)
        {
            Interlocked.Increment(ref _total);
            if (statusCode >= 500) Interlocked.Increment(ref _errors);
            lock (_lock) _latencySum += latencySeconds;
        }

        public long   Total     => Interlocked.Read(ref _total);
        public long   Errors    => Interlocked.Read(ref _errors);
        public double LatencyMs
        {
            get
            {
                lock (_lock)
                {
                    var t = Interlocked.Read(ref _total);
                    return t == 0 ? 0 : (_latencySum / t) * 1000.0;
                }
            }
        }
    }
}

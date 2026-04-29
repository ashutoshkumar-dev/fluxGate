using Gateway.Core.Interfaces;
using StackExchange.Redis;

namespace Gateway.API.Middleware;

/// <summary>
/// Sliding-window rate limiter using Redis INCR + EXPIRE.
/// Key: rate_limit:{userId}:{normalizedPath}
/// If the route has no RateLimit config, the request passes through.
/// Exceeding the limit returns 429 with a Retry-After header.
/// </summary>
public sealed class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDatabase _redis;

    public RateLimitMiddleware(RequestDelegate next, IConnectionMultiplexer redis)
    {
        _next  = next;
        _redis = redis.GetDatabase();
    }

    public async Task InvokeAsync(HttpContext ctx, IRouteRepository routeRepo)
    {
        var path   = ctx.Request.Path.Value ?? "/";
        var method = ctx.Request.Method;

        // Find matching active route (longest-prefix match, same logic as RouteAuthorizationMiddleware)
        var routes = await routeRepo.GetAllAsync(true, ctx.RequestAborted);
        var route  = routes
            .Where(r => (r.Method == "*" || r.Method.Equals(method, StringComparison.OrdinalIgnoreCase))
                     && path.StartsWith(r.Path, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.Path.Length)
            .FirstOrDefault();

        // No matching route OR no rate-limit config → pass through
        if (route?.RateLimit is null || route.RateLimit.Limit <= 0)
        {
            await _next(ctx);
            return;
        }

        var rl = route.RateLimit;

        // Identify requester: authenticated user id or remote IP
        var userId = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? ctx.Connection.RemoteIpAddress?.ToString()
                     ?? "anonymous";

        var normalizedPath = route.Path.TrimStart('/');
        var key            = $"rate_limit:{userId}:{normalizedPath}";

        // Atomic increment; set TTL only on first request in window
        var count = await _redis.StringIncrementAsync(key);
        if (count == 1)
            await _redis.KeyExpireAsync(key, TimeSpan.FromSeconds(rl.WindowSeconds));

        if (count > rl.Limit)
        {
            // Calculate remaining TTL for Retry-After header
            var ttl         = await _redis.KeyTimeToLiveAsync(key);
            var retryAfter  = (int)Math.Ceiling(ttl?.TotalSeconds ?? rl.WindowSeconds);

            ctx.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            ctx.Response.Headers["Retry-After"] = retryAfter.ToString();
            ctx.Response.Headers["X-RateLimit-Limit"]     = rl.Limit.ToString();
            ctx.Response.Headers["X-RateLimit-Remaining"] = "0";
            await ctx.Response.WriteAsync("Rate limit exceeded. Try again later.");
            return;
        }

        ctx.Response.Headers["X-RateLimit-Limit"]     = rl.Limit.ToString();
        ctx.Response.Headers["X-RateLimit-Remaining"] = Math.Max(0, rl.Limit - (int)count).ToString();

        await _next(ctx);
    }
}

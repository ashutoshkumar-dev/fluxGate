using System.Security.Cryptography;
using System.Text;
using Gateway.Core.Interfaces;
using StackExchange.Redis;

namespace Gateway.API.Middleware;

/// <summary>
/// Redis-backed response cache middleware.
/// Must run AFTER rate limiting and BEFORE YARP.
///
/// Cache key:   cache:{METHOD}:{path}:{queryHash}
/// TTL:         route.CacheTtlSeconds
/// Bypass:      Cache-Control: no-cache header
/// Cacheable:   GET / HEAD only, 2xx responses only
/// </summary>
public sealed class ResponseCacheMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDatabase _redis;

    public ResponseCacheMiddleware(RequestDelegate next, IConnectionMultiplexer redis)
    {
        _next  = next;
        _redis = redis.GetDatabase();
    }

    public async Task InvokeAsync(HttpContext ctx, IRouteRepository routeRepo)
    {
        // Only GET / HEAD can be cached
        if (!HttpMethods.IsGet(ctx.Request.Method) && !HttpMethods.IsHead(ctx.Request.Method))
        {
            await _next(ctx);
            return;
        }

        // Cache-Control: no-cache bypasses
        if (ctx.Request.Headers.CacheControl.ToString().Contains("no-cache",
                StringComparison.OrdinalIgnoreCase))
        {
            await _next(ctx);
            return;
        }

        var path   = ctx.Request.Path.Value ?? "/";
        var method = ctx.Request.Method.ToUpperInvariant();

        // Find the matching route and its cache TTL
        var routes = await routeRepo.GetAllAsync(isActive: true, ctx.RequestAborted);
        var matched = routes
            .Where(r =>
                path.StartsWith(r.Path, StringComparison.OrdinalIgnoreCase) &&
                (r.Method == "*" || r.Method.Equals(method, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(r => r.Path.Length)
            .FirstOrDefault();

        if (matched?.CacheTtlSeconds is null or <= 0)
        {
            await _next(ctx);
            return;
        }

        var cacheKey = BuildKey(method, path, ctx.Request.QueryString.Value ?? "");

        // Cache hit → return cached response
        var cached = await _redis.StringGetAsync(cacheKey);
        if (cached.HasValue)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            ctx.Response.Headers["X-Cache"] = "HIT";
            await ctx.Response.WriteAsync(cached!);
            return;
        }

        // Cache miss → capture response body
        var originalBody = ctx.Response.Body;
        await using var buffer = new MemoryStream();
        ctx.Response.Body = buffer;

        try
        {
            await _next(ctx);
        }
        finally
        {
            ctx.Response.Body = originalBody;
        }

        buffer.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(buffer).ReadToEndAsync();

        // Only cache successful responses
        if (ctx.Response.StatusCode is >= 200 and < 300)
        {
            await _redis.StringSetAsync(
                cacheKey,
                responseBody,
                TimeSpan.FromSeconds(matched.CacheTtlSeconds.Value));
            ctx.Response.Headers["X-Cache"] = "MISS";
        }

        await ctx.Response.WriteAsync(responseBody);
    }

    private static string BuildKey(string method, string path, string query)
    {
        var queryHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(query)))[..8];
        return $"cache:{method}:{path.TrimEnd('/')}:{queryHash}";
    }
}

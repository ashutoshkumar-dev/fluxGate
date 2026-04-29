using System.Net;
using System.Security.Claims;
using FluentAssertions;
using Gateway.API.Middleware;
using Gateway.Core.Domain.Entities;
using Gateway.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Moq;
using StackExchange.Redis;

namespace Gateway.Tests.RateLimit;

/// <summary>
/// Unit tests for RateLimitMiddleware using a mock IDatabase.
/// </summary>
public class RateLimitMiddlewareTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static Route MakeRoute(string path, int limit, int windowSeconds) => new()
    {
        Id          = Guid.NewGuid(),
        Path        = path,
        Method      = "GET",
        Destination = "http://up",
        IsActive    = true,
        RateLimit   = new RateLimitConfig { Limit = limit, WindowSeconds = windowSeconds }
    };

    private static (RateLimitMiddleware mw, Mock<IRouteRepository> repo, Mock<IDatabase> db)
        Build(RequestDelegate next, long incrResult, TimeSpan? ttl = null)
    {
        var repo     = new Mock<IRouteRepository>();
        var db       = new Mock<IDatabase>();
        var muxMock  = new Mock<IConnectionMultiplexer>();
        muxMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);

        db.Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), 1, CommandFlags.None))
          .ReturnsAsync(incrResult);
        db.Setup(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), CommandFlags.None))
          .ReturnsAsync(true);
        db.Setup(d => d.KeyTimeToLiveAsync(It.IsAny<RedisKey>(), CommandFlags.None))
          .ReturnsAsync(ttl ?? TimeSpan.FromSeconds(55));

        return (new RateLimitMiddleware(next, muxMock.Object), repo, db);
    }

    private static HttpContext MakeContext(string path, string method = "GET", string? userId = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path   = path;
        ctx.Request.Method = method;
        ctx.Response.Body  = new MemoryStream();

        if (userId is not null)
        {
            var claims    = new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
            ctx.User      = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        }
        return ctx;
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NoMatchingRoute_PassesThrough()
    {
        var called    = false;
        var (mw, repo, _) = Build(_ => { called = true; return Task.CompletedTask; }, incrResult: 1);
        repo.Setup(r => r.GetAllAsync(true, default)).ReturnsAsync([]);

        var ctx = MakeContext("/no-match");
        await mw.InvokeAsync(ctx, repo.Object);

        called.Should().BeTrue();
    }

    [Fact]
    public async Task RouteWithNoRateLimitConfig_PassesThrough()
    {
        var called = false;
        var (mw, repo, _) = Build(_ => { called = true; return Task.CompletedTask; }, incrResult: 1);
        var route = MakeRoute("/api", 5, 60);
        route.RateLimit = null;   // explicitly null
        repo.Setup(r => r.GetAllAsync(true, default)).ReturnsAsync([route]);

        var ctx = MakeContext("/api/data");
        await mw.InvokeAsync(ctx, repo.Object);

        called.Should().BeTrue();
    }

    [Fact]
    public async Task WithinLimit_PassesThrough_AndSetsHeaders()
    {
        var called = false;
        var (mw, repo, _) = Build(_ => { called = true; return Task.CompletedTask; }, incrResult: 3);
        repo.Setup(r => r.GetAllAsync(true, default)).ReturnsAsync([MakeRoute("/api", 5, 60)]);

        var ctx = MakeContext("/api/data", userId: "user1");
        await mw.InvokeAsync(ctx, repo.Object);

        called.Should().BeTrue();
        ctx.Response.Headers["X-RateLimit-Limit"].ToString().Should().Be("5");
        ctx.Response.Headers["X-RateLimit-Remaining"].ToString().Should().Be("2");
    }

    [Fact]
    public async Task ExceedsLimit_Returns429_WithRetryAfterHeader()
    {
        var (mw, repo, _) = Build(_ => Task.CompletedTask, incrResult: 6, ttl: TimeSpan.FromSeconds(42));
        repo.Setup(r => r.GetAllAsync(true, default)).ReturnsAsync([MakeRoute("/api", 5, 60)]);

        var ctx = MakeContext("/api/data", userId: "user1");
        await mw.InvokeAsync(ctx, repo.Object);

        ctx.Response.StatusCode.Should().Be(429);
        ctx.Response.Headers["Retry-After"].ToString().Should().Be("42");
        ctx.Response.Headers["X-RateLimit-Limit"].ToString().Should().Be("5");
        ctx.Response.Headers["X-RateLimit-Remaining"].ToString().Should().Be("0");
    }

    [Fact]
    public async Task DifferentUsers_HaveIndependentCounters()
    {
        // Both users at count=3 (within limit=5) — both should pass
        var calls   = 0;
        var db      = new Mock<IDatabase>();
        var muxMock = new Mock<IConnectionMultiplexer>();
        muxMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);
        db.Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), 1, CommandFlags.None))
          .ReturnsAsync(3L);
        db.Setup(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), CommandFlags.None))
          .ReturnsAsync(true);

        var repo  = new Mock<IRouteRepository>();
        repo.Setup(r => r.GetAllAsync(true, default)).ReturnsAsync([MakeRoute("/api", 5, 60)]);

        var mw = new RateLimitMiddleware(_ => { calls++; return Task.CompletedTask; }, muxMock.Object);

        // User A
        var ctxA = MakeContext("/api/x", userId: "user-a");
        await mw.InvokeAsync(ctxA, repo.Object);

        // User B
        var ctxB = MakeContext("/api/x", userId: "user-b");
        await mw.InvokeAsync(ctxB, repo.Object);

        calls.Should().Be(2, "both users are within their own limit");

        // Verify distinct Redis keys were used
        db.Verify(d => d.StringIncrementAsync(
            It.Is<RedisKey>(k => k.ToString().Contains("user-a")), 1, CommandFlags.None), Times.Once);
        db.Verify(d => d.StringIncrementAsync(
            It.Is<RedisKey>(k => k.ToString().Contains("user-b")), 1, CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task ExactlyAtLimit_PassesThrough()
    {
        var called    = false;
        var (mw, repo, _) = Build(_ => { called = true; return Task.CompletedTask; }, incrResult: 5);
        repo.Setup(r => r.GetAllAsync(true, default)).ReturnsAsync([MakeRoute("/api", 5, 60)]);

        var ctx = MakeContext("/api/data", userId: "user1");
        await mw.InvokeAsync(ctx, repo.Object);

        called.Should().BeTrue("count==limit should still pass");
        ctx.Response.StatusCode.Should().NotBe(429);
    }
}

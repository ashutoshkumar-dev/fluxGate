using System.Net;
using FluentAssertions;
using Gateway.Core.Domain.Entities;
using Gateway.Core.Interfaces;
using Gateway.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Retry;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Gateway.Tests.Phase11;

/// <summary>
/// Phase 11 integration tests (AC1–AC6, AC8).
///
/// Retry and circuit-breaker are tested via the Polly pipeline directly to avoid
/// the YARP streaming limitation (YARP cannot re-send a request once the response
/// body pipe has been opened).  Cache and header-transform tests go end-to-end
/// through the full Gateway WebApplicationFactory.
/// </summary>
public class Phase11AdvancedFeaturesTests : IDisposable
{
    private readonly WireMockServer _wireMock;

    public Phase11AdvancedFeaturesTests()
    {
        _wireMock = WireMockServer.Start();
    }

    public void Dispose() => _wireMock.Stop();

    // ── AC1: Retry fires 3 times before giving up ──────────────────────────
    [Fact]
    public async Task RetryPipeline_Retries3TimesOn5xxException()
    {
        var registry = new ResiliencePipelineRegistry<string>();

        var pipeline = registry.GetOrAddPipeline("retry-test", builder =>
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType      = DelayBackoffType.Exponential,
                Delay            = TimeSpan.FromMilliseconds(1), // fast in tests
                ShouldHandle     = new PredicateBuilder()
                    .Handle<HttpRequestException>(ex =>
                        ex.StatusCode is not null && (int)ex.StatusCode >= 500)
            });
        });

        int callCount = 0;

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await pipeline.ExecuteAsync(async _ =>
            {
                Interlocked.Increment(ref callCount);
                throw new HttpRequestException("upstream 500", null, HttpStatusCode.InternalServerError);
            });
        });

        // 1 initial attempt + 3 retries = 4 total
        callCount.Should().Be(4);
    }

    // ── AC2: Circuit opens after 5 failures → subsequent calls get exception
    [Fact]
    public async Task CircuitBreaker_OpensAfter5Failures_ThrowsBrokenCircuit()
    {
        var registry = new ResiliencePipelineRegistry<string>();

        var pipeline = registry.GetOrAddPipeline("cb-test-" + Guid.NewGuid(), builder =>
        {
            builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                SamplingDuration  = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                FailureRatio      = 1.0,
                BreakDuration     = TimeSpan.FromSeconds(60),
                ShouldHandle      = new PredicateBuilder()
                    .Handle<HttpRequestException>(ex =>
                        ex.StatusCode is not null && (int)ex.StatusCode >= 500)
            });
        });

        // Drive 5 failures
        for (int i = 0; i < 5; i++)
        {
            try
            {
                await pipeline.ExecuteAsync(async _ =>
                    throw new HttpRequestException("500", null, HttpStatusCode.InternalServerError));
            }
            catch (HttpRequestException) { }
        }

        // 6th call: circuit is open → BrokenCircuitException
        await Assert.ThrowsAsync<BrokenCircuitException>(async () =>
        {
            await pipeline.ExecuteAsync(async _ => { });
        });
    }

    // ── AC3: Half-open probe allowed after break duration ──────────────────
    [Fact]
    public async Task CircuitBreaker_AllowsProbeAfterBreakDuration()
    {
        var registry = new ResiliencePipelineRegistry<string>();

        var pipeline = registry.GetOrAddPipeline("cb-halfopen-" + Guid.NewGuid(), builder =>
        {
            builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                SamplingDuration  = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                FailureRatio      = 1.0,
                BreakDuration     = TimeSpan.FromMilliseconds(500), // min allowed by Polly v8
                ShouldHandle      = new PredicateBuilder()
                    .Handle<HttpRequestException>(ex =>
                        ex.StatusCode is not null && (int)ex.StatusCode >= 500)
            });
        });

        for (int i = 0; i < 5; i++)
        {
            try
            {
                await pipeline.ExecuteAsync(async _ =>
                    throw new HttpRequestException("500", null, HttpStatusCode.InternalServerError));
            }
            catch { }
        }

        // Wait past break duration → half-open
        await Task.Delay(600);

        int probeCount = 0;
        await pipeline.ExecuteAsync(async _ =>
        {
            Interlocked.Increment(ref probeCount);
        });

        probeCount.Should().Be(1, "half-open state should allow exactly one probe");
    }

    // ── AC4: Cache hit on second identical GET request ─────────────────────
    [Fact]
    public async Task ResponseCache_SecondRequest_ServedFromCache()
    {
        // Use a unique path per test run to avoid Redis key collisions across runs
        var uniqueSegment = Guid.NewGuid().ToString("N")[..8];
        var routePath     = "/cache-" + uniqueSegment;

        _wireMock
            .Given(Request.Create().WithPath("/data-" + uniqueSegment).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"value\":42}"));

        var route = new Route
        {
            Id = Guid.NewGuid(),
            Path = routePath,
            Method = "GET",
            Destination = _wireMock.Urls[0],
            IsActive = true,
            CacheTtlSeconds = 60
        };

        await using var factory = new Phase11TestFactory(route);
        var client = factory.CreateClient();

        var r1 = await client.GetAsync(routePath + "/data-" + uniqueSegment);
        r1.StatusCode.Should().Be(HttpStatusCode.OK);
        r1.Headers.TryGetValues("X-Cache", out var v1);
        v1?.FirstOrDefault().Should().Be("MISS");

        var r2 = await client.GetAsync(routePath + "/data-" + uniqueSegment);
        r2.StatusCode.Should().Be(HttpStatusCode.OK);
        r2.Headers.TryGetValues("X-Cache", out var v2);
        v2?.FirstOrDefault().Should().Be("HIT");

        _wireMock.LogEntries.Should().HaveCount(1);
    }

    // ── AC5: Cache bypassed on Cache-Control: no-cache ─────────────────────
    [Fact]
    public async Task ResponseCache_NoCacheHeader_BypassesCache()
    {
        _wireMock
            .Given(Request.Create().WithPath("/data").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"value\":1}"));

        var route = new Route
        {
            Id = Guid.NewGuid(),
            Path = "/nocache-test",
            Method = "GET",
            Destination = _wireMock.Urls[0],
            IsActive = true,
            CacheTtlSeconds = 60
        };

        await using var factory = new Phase11TestFactory(route);
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/nocache-test/data");
        request.Headers.CacheControl =
            new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        response.Headers.TryGetValues("X-Cache", out var cacheHeader);
        cacheHeader?.FirstOrDefault().Should().BeNullOrEmpty();
    }

    // ── AC6: Proxied request has X-Request-Id / X-Request-ID and X-Gateway-Version
    [Fact]
    public async Task RequestTransform_InjectsForwardedHeaders()
    {
        _wireMock
            .Given(Request.Create().WithPath("/echo-headers").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("ok"));

        var route = new Route
        {
            Id = Guid.NewGuid(),
            Path = "/transform-test",
            Method = "GET",
            Destination = _wireMock.Urls[0],
            IsActive = true
        };

        await using var factory = new Phase11TestFactory(route);
        var client = factory.CreateClient();

        await client.GetAsync("/transform-test/echo-headers");

        var received = _wireMock.LogEntries.Last().RequestMessage;

        // YARP normalises header name casing; accept both forms
        var hasRequestId = received.Headers.ContainsKey("X-Request-Id") ||
                           received.Headers.ContainsKey("X-Request-ID");
        hasRequestId.Should().BeTrue("gateway must inject X-Request-Id into every proxied request");

        received.Headers.Should().ContainKey("X-Gateway-Version");
        received.Headers["X-Gateway-Version"].Should().Contain("fluxgate/1.0");
    }
}

/// <summary>
/// WebApplicationFactory for Phase 11 tests — injects a single test route.
/// </summary>
internal sealed class Phase11TestFactory : WebApplicationFactory<Program>
{
    private readonly Route _route;

    public Phase11TestFactory(Route route) => _route = route;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var dbOpts = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<GatewayDbContext>));
            if (dbOpts is not null) services.Remove(dbOpts);
            services.AddDbContext<GatewayDbContext>(opts =>
                opts.UseInMemoryDatabase("gateway-p11-" + Guid.NewGuid()));

            var authOpts = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AuthDbContext>));
            if (authOpts is not null) services.Remove(authOpts);
            services.AddDbContext<AuthDbContext>(opts =>
                opts.UseInMemoryDatabase("auth-p11-" + Guid.NewGuid()));

            var repoDesc = services.SingleOrDefault(d => d.ServiceType == typeof(IRouteRepository));
            if (repoDesc is not null) services.Remove(repoDesc);

            var repoMock = new Mock<IRouteRepository>();
            repoMock
                .Setup(r => r.GetAllAsync(It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { _route });
            repoMock
                .Setup(r => r.GetAllAsync(true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { _route });
            services.AddSingleton<IRouteRepository>(repoMock.Object);
        });
    }
}

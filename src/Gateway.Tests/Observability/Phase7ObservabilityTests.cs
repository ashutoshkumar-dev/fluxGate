using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Gateway.API.Metrics;
using Gateway.Core.DTOs;
using Gateway.Core.Interfaces;
using Gateway.Infrastructure.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Gateway.Tests.Observability;

// ── Unit tests ────────────────────────────────────────────────────────────────

public class MetricsRegistryTests
{
    [Fact]
    public void Record_AccumulatesCountAndLatency()
    {
        var registry = new MetricsRegistry();
        registry.Record("GET", "/api", 200, 0.05);
        registry.Record("GET", "/api", 200, 0.10);
        registry.Record("GET", "/api", 500, 0.20);

        var snap = registry.Snapshot();
        snap.Should().HaveCount(1);

        var stats = snap[0];
        stats.Total.Should().Be(3);
        stats.Errors.Should().Be(1);
        stats.LatencyMs.Should().BeApproximately(116.67, 1.0); // avg of 50ms, 100ms, 200ms
    }

    [Fact]
    public void Record_DifferentRoutes_TrackedSeparately()
    {
        var registry = new MetricsRegistry();
        registry.Record("GET", "/a", 200, 0.01);
        registry.Record("POST", "/b", 201, 0.02);

        registry.Snapshot().Should().HaveCount(2);
    }

    [Fact]
    public void Record_ErrorsOnly5xx()
    {
        var registry = new MetricsRegistry();
        registry.Record("GET", "/x", 200, 0.01);
        registry.Record("GET", "/x", 400, 0.01);
        registry.Record("GET", "/x", 499, 0.01);
        registry.Record("GET", "/x", 500, 0.01);
        registry.Record("GET", "/x", 503, 0.01);

        var stats = registry.Snapshot()[0];
        stats.Total.Should().Be(5);
        stats.Errors.Should().Be(2);
    }
}

public class MetricsSummaryTests
{
    [Fact]
    public void Summary_EmptyRegistry_ReturnsZeros()
    {
        var registry = new MetricsRegistry();
        var snap = registry.Snapshot();

        snap.Sum(r => r.Total).Should().Be(0);
        snap.Sum(r => r.Errors).Should().Be(0);
    }

    [Fact]
    public void Summary_ErrorRateCalculation()
    {
        var registry = new MetricsRegistry();
        for (int i = 0; i < 8; i++) registry.Record("GET", "/api", 200, 0.01);
        for (int i = 0; i < 2; i++) registry.Record("GET", "/api", 500, 0.01);

        var snap = registry.Snapshot();
        var total  = snap.Sum(r => r.Total);
        var errors = snap.Sum(r => r.Errors);
        var rate   = (double)errors / total;

        total.Should().Be(10);
        errors.Should().Be(2);
        rate.Should().BeApproximately(0.2, 0.001);
    }
}

// ── SeqLogsService unit tests (with mocked HTTP) ─────────────────────────────

public class SeqLogsServiceTests
{
    private static SeqLogsService BuildService(HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("seq"))
               .Returns(new HttpClient(handler) { BaseAddress = null });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Seq:ServerUrl"] = "http://seq-test" })
            .Build();

        return new SeqLogsService(factory.Object, config, NullLogger<SeqLogsService>.Instance);
    }

    [Fact]
    public async Task QueryLogs_SeqUnavailable_ReturnsEmptyPage()
    {
        var handler = new AlwaysFailHandler();
        var svc = BuildService(handler);

        var result = await svc.QueryLogsAsync(new LogQueryDto { Page = 1, PageSize = 10 });

        result.Should().NotBeNull();
        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task QueryLogs_ValidSeqResponse_MapsEventFields()
    {
        var seqJson = """
            [
              {
                "Timestamp": "2026-04-29T12:00:00+00:00",
                "Level": "Information",
                "MessageTemplateTokens": [
                  { "Text": "HTTP GET /health responded 200" }
                ],
                "Properties": [
                  { "Name": "RequestPath",   "Value": "/health" },
                  { "Name": "RequestMethod", "Value": "GET" },
                  { "Name": "StatusCode",    "Value": 200 },
                  { "Name": "Elapsed",       "Value": 12.5 }
                ]
              }
            ]
            """;

        var handler = new StaticJsonHandler(seqJson);
        var svc = BuildService(handler);

        var result = await svc.QueryLogsAsync(new LogQueryDto { Page = 1, PageSize = 50 });

        result.Items.Should().HaveCount(1);
        var entry = result.Items[0];
        entry.Level.Should().Be("Information");
        entry.Route.Should().Be("/health");
        entry.Method.Should().Be("GET");
        entry.StatusCode.Should().Be(200);
        entry.LatencyMs.Should().Be(12.5);
    }

    [Fact]
    public async Task QueryLogs_StatusFilter_BuildsCorrectUrl()
    {
        string? capturedUrl = null;
        var handler = new CapturingHandler(url =>
        {
            capturedUrl = url;
            return "[]";
        });
        var svc = BuildService(handler);

        await svc.QueryLogsAsync(new LogQueryDto { Status = "error", PageSize = 10 });

        capturedUrl.Should().Contain("filter=");
        capturedUrl.Should().Contain("Error");
    }

    [Fact]
    public async Task QueryLogs_RouteFilter_BuildsCorrectUrl()
    {
        string? capturedUrl = null;
        var handler = new CapturingHandler(url =>
        {
            capturedUrl = url;
            return "[]";
        });
        var svc = BuildService(handler);

        await svc.QueryLogsAsync(new LogQueryDto { Route = "/orders", PageSize = 10 });

        capturedUrl.Should().Contain("filter=");
        capturedUrl.Should().Contain("orders");
    }

    // ── Test HTTP handlers ─────────────────────────────────────────────────

    private sealed class AlwaysFailHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
            => throw new HttpRequestException("Seq unavailable (test)");
    }

    private sealed class StaticJsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(resp);
        }
    }

    private sealed class CapturingHandler(Func<string, string> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            var body = respond(req.RequestUri?.ToString() ?? "");
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(resp);
        }
    }
}

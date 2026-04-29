using System.Net;
using FluentAssertions;
using Gateway.API.Proxy;
using Gateway.Core.Domain.Entities;
using Gateway.Core.Interfaces;
using Gateway.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Yarp.ReverseProxy.Configuration;

namespace Gateway.Tests.Proxy;

/// <summary>
/// Integration test: requests made to the Gateway are proxied to a WireMock upstream
/// whose address comes from a route stored in the (mocked) database.
/// </summary>
public class YarpIntegrationTests : IDisposable
{
    private readonly WireMockServer _wireMock;

    public YarpIntegrationTests()
    {
        _wireMock = WireMockServer.Start();

        _wireMock
            .Given(Request.Create().WithPath("/ping").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("pong from upstream"));
    }

    public void Dispose() => _wireMock.Stop();

    [Fact]
    public async Task Gateway_ProxiesRequest_ToCorrectUpstream()
    {
        var routeId = Guid.NewGuid();
        var testRoute = new Route
        {
            Id = routeId,
            Path = "/upstream",
            Method = "GET",
            Destination = _wireMock.Urls[0],
            IsActive = true
        };

        await using var factory = new GatewayTestFactory(testRoute);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/upstream/ping");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("pong from upstream");
    }

    [Fact]
    public async Task Gateway_AfterRouteNotify_ProxiesToNewUpstream()
    {
        // Second WireMock for the new upstream
        using var wireMock2 = WireMockServer.Start();
        wireMock2
            .Given(Request.Create().WithPath("/ping").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("pong from upstream-2"));

        var routeId = Guid.NewGuid();
        var initialRoute = new Route
        {
            Id = routeId,
            Path = "/upstream",
            Method = "GET",
            Destination = _wireMock.Urls[0],
            IsActive = true
        };
        var updatedRoute = new Route
        {
            Id = routeId,
            Path = "/upstream",
            Method = "GET",
            Destination = wireMock2.Urls[0],
            IsActive = true
        };

        var callCount = 0;
        var repoMock = new Mock<IRouteRepository>();
        repoMock.Setup(r => r.GetAllAsync(true, default))
                .ReturnsAsync(() => callCount++ == 0
                    ? (IEnumerable<Route>)[initialRoute]
                    : [updatedRoute]);

        await using var factory = new GatewayTestFactory(repoMock.Object);
        var client = factory.CreateClient();

        // First request: proxied to wireMock1
        var resp1 = await client.GetAsync("/upstream/ping");
        (await resp1.Content.ReadAsStringAsync()).Should().Be("pong from upstream");

        // Trigger hot-reload
        using var scope = factory.Services.CreateScope();
        var notifier = scope.ServiceProvider.GetRequiredService<RouteChangeNotifier>();
        notifier.NotifyChange();

        // Give YARP a moment to apply the new config
        await Task.Delay(200);

        // Second request: proxied to wireMock2
        var resp2 = await client.GetAsync("/upstream/ping");
        (await resp2.Content.ReadAsStringAsync()).Should().Be("pong from upstream-2");
    }
}

/// <summary>
/// WebApplicationFactory that replaces IRouteRepository with a controlled fake
/// so YARP's DatabaseProxyConfigProvider uses test routes instead of a real DB.
/// </summary>
internal sealed class GatewayTestFactory : WebApplicationFactory<Program>
{
    private readonly IRouteRepository? _repo;
    private readonly Route? _singleRoute;

    public GatewayTestFactory(Route singleRoute) => _singleRoute = singleRoute;
    public GatewayTestFactory(IRouteRepository repo) => _repo = repo;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove real DbContexts (avoids needing a real postgres connection)
            var dbOpts = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<GatewayDbContext>));
            if (dbOpts is not null) services.Remove(dbOpts);
            services.AddDbContext<GatewayDbContext>(opts =>
                opts.UseInMemoryDatabase("gateway-test"));

            var authOpts = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AuthDbContext>));
            if (authOpts is not null) services.Remove(authOpts);
            services.AddDbContext<AuthDbContext>(opts =>
                opts.UseInMemoryDatabase("auth-test"));

            // Replace IRouteRepository with mock
            var repoDesc = services.SingleOrDefault(d => d.ServiceType == typeof(IRouteRepository));
            if (repoDesc is not null) services.Remove(repoDesc);

            if (_repo is not null)
            {
                services.AddSingleton(_repo);
            }
            else
            {
                var repoMock = new Mock<IRouteRepository>();
                repoMock.Setup(r => r.GetAllAsync(true, default))
                        .ReturnsAsync(_singleRoute is not null
                            ? (IEnumerable<Route>)[_singleRoute]
                            : []);
                services.AddSingleton<IRouteRepository>(repoMock.Object);
            }
        });
    }
}

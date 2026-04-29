using FluentAssertions;
using Gateway.API.Proxy;
using Gateway.Core.Domain.Entities;
using Gateway.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Gateway.Tests.Proxy;

public class ProxyConfigProviderTests
{
    private static IServiceScopeFactory BuildScopeFactory(IRouteRepository repo)
    {
        var services = new ServiceCollection();
        services.AddSingleton(repo);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    [Fact]
    public void GetConfig_ActiveRoutes_MappedToYarpFormat()
    {
        var route = new Route
        {
            Id = Guid.NewGuid(),
            Path = "/api/users",
            Method = "GET",
            Destination = "http://user-service:8080",
            IsActive = true
        };

        var repoMock = new Mock<IRouteRepository>();
        repoMock.Setup(r => r.GetAllAsync(true, default)).ReturnsAsync([route]);

        var notifier = new RouteChangeNotifier();
        var provider = new DatabaseProxyConfigProvider(BuildScopeFactory(repoMock.Object), notifier);

        var config = provider.GetConfig();

        config.Routes.Should().HaveCount(1);
        config.Routes[0].RouteId.Should().Be(route.Id.ToString());
        config.Routes[0].Match.Path.Should().StartWith(route.Path);
        config.Routes[0].Match.Methods.Should().ContainSingle().Which.Should().Be("GET");

        config.Clusters.Should().HaveCount(1);
        config.Clusters[0].ClusterId.Should().Be($"cluster-{route.Id}");
        config.Clusters[0].Destinations!["primary"].Address.Should().Be("http://user-service:8080");
    }

    [Fact]
    public void GetConfig_RouteHasPathRemovePrefix_Transform()
    {
        var route = new Route
        {
            Id = Guid.NewGuid(),
            Path = "/api/orders",
            Method = "GET",
            Destination = "http://order-svc",
            IsActive = true
        };

        var repoMock = new Mock<IRouteRepository>();
        repoMock.Setup(r => r.GetAllAsync(true, default)).ReturnsAsync([route]);

        var provider = new DatabaseProxyConfigProvider(
            BuildScopeFactory(repoMock.Object), new RouteChangeNotifier());

        var config = provider.GetConfig();

        var transforms = config.Routes[0].Transforms;
        transforms.Should().NotBeNullOrEmpty("each route must strip its prefix before forwarding");
        transforms!.Should().ContainSingle(t =>
            t.ContainsKey("PathRemovePrefix") && t["PathRemovePrefix"] == "/api/orders",
            "prefix must equal the route path without trailing slash");
    }

    [Fact]
    public void GetConfig_WildcardMethod_PassesNullToYarp()
    {
        var route = new Route
        {
            Id = Guid.NewGuid(), Path = "/any", Method = "*",
            Destination = "http://svc", IsActive = true
        };
        var repoMock = new Mock<IRouteRepository>();
        repoMock.Setup(r => r.GetAllAsync(true, default)).ReturnsAsync([route]);

        var config = new DatabaseProxyConfigProvider(
            BuildScopeFactory(repoMock.Object), new RouteChangeNotifier()).GetConfig();

        config.Routes[0].Match.Methods.Should().BeNull(
            "null tells YARP to match any HTTP method");
    }

    [Fact]
    public void GetConfig_OnlyActiveRoutes_AreIncluded()
    {
        // Provider calls GetAllAsync(isActive: true) — verify it passes true
        var repoMock = new Mock<IRouteRepository>();
        repoMock.Setup(r => r.GetAllAsync(true, default)).ReturnsAsync([]);

        var provider = new DatabaseProxyConfigProvider(
            BuildScopeFactory(repoMock.Object), new RouteChangeNotifier());

        provider.GetConfig();

        repoMock.Verify(r => r.GetAllAsync(true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GetConfig_AfterNotifyChange_ReturnsRefreshedConfig()
    {
        var route1 = new Route
        {
            Id = Guid.NewGuid(), Path = "/a", Method = "GET",
            Destination = "http://svc-a", IsActive = true
        };
        var route2 = new Route
        {
            Id = Guid.NewGuid(), Path = "/b", Method = "GET",
            Destination = "http://svc-b", IsActive = true
        };

        var repoMock = new Mock<IRouteRepository>();
        repoMock.SetupSequence(r => r.GetAllAsync(true, default))
                .ReturnsAsync([route1])
                .ReturnsAsync([route1, route2]);

        var notifier = new RouteChangeNotifier();
        var provider = new DatabaseProxyConfigProvider(BuildScopeFactory(repoMock.Object), notifier);

        var config1 = provider.GetConfig();
        config1.Routes.Should().HaveCount(1);

        // Simulate YARP detecting a route change
        notifier.NotifyChange();

        // After token fires, GetConfig() should reload from DB
        var config2 = provider.GetConfig();
        config2.Routes.Should().HaveCount(2);
        config2.Should().NotBeSameAs(config1);
    }

    [Fact]
    public void GetConfig_CachedOnSubsequentCalls_NoExtraDbHits()
    {
        var repoMock = new Mock<IRouteRepository>();
        repoMock.Setup(r => r.GetAllAsync(true, default)).ReturnsAsync([]);

        var provider = new DatabaseProxyConfigProvider(
            BuildScopeFactory(repoMock.Object), new RouteChangeNotifier());

        provider.GetConfig();
        provider.GetConfig();
        provider.GetConfig();

        // DB should only be hit once (not three times)
        repoMock.Verify(r => r.GetAllAsync(true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void NotifyChange_FiresChangeToken_OnCurrentConfig()
    {
        var repoMock = new Mock<IRouteRepository>();
        repoMock.Setup(r => r.GetAllAsync(true, default)).ReturnsAsync([]);

        var notifier = new RouteChangeNotifier();
        var provider = new DatabaseProxyConfigProvider(BuildScopeFactory(repoMock.Object), notifier);

        var config = provider.GetConfig();
        config.ChangeToken.HasChanged.Should().BeFalse();

        notifier.NotifyChange();

        config.ChangeToken.HasChanged.Should().BeTrue();
    }
}

using Gateway.Core.Interfaces;
using Yarp.ReverseProxy.Configuration;

namespace Gateway.API.Proxy;

/// <summary>
/// Loads active routes from the database and exposes them as YARP configuration.
/// After a route CUD, RouteChangeNotifier.NotifyChange() fires the current config's
/// change token and YARP calls GetConfig() again to pick up the new snapshot.
/// </summary>
public sealed class DatabaseProxyConfigProvider : IProxyConfigProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RouteChangeNotifier _notifier;
    private volatile FluxgateProxyConfig? _currentConfig;

    public DatabaseProxyConfigProvider(
        IServiceScopeFactory scopeFactory,
        RouteChangeNotifier notifier)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
    }

    public IProxyConfig GetConfig()
    {
        // Load on first call or after the change token has fired
        if (_currentConfig is null || _currentConfig.ChangeToken.HasChanged)
        {
            var newConfig = BuildConfig();
            _currentConfig = newConfig;
            _notifier.SetCurrentConfig(newConfig);
        }

        return _currentConfig;
    }

    private FluxgateProxyConfig BuildConfig()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRouteRepository>();

        // Blocking call is acceptable here; IProxyConfigProvider is synchronous by contract
        var routes = repo.GetAllAsync(isActive: true).GetAwaiter().GetResult();

        var yarpRoutes = new List<RouteConfig>();
        var yarpClusters = new List<ClusterConfig>();

        foreach (var route in routes)
        {
            var routeId = route.Id.ToString();
            var clusterId = $"cluster-{routeId}";

            yarpRoutes.Add(new RouteConfig
            {
                RouteId = routeId,
                ClusterId = clusterId,
                Match = new RouteMatch
                {
                    // Trailing /{**remainder} ensures prefix matching covers both
                    // /api/users  and  /api/users/123  in one pattern
                    Path = route.Path.TrimEnd('/') + "/{**remainder}",
                    Methods = route.Method == "*" ? null : [route.Method]
                },
                // Strip the route prefix before forwarding to the upstream:
                // /api/users/123  →  /123  at the destination
                Transforms =
                [
                    new Dictionary<string, string>
                    {
                        ["PathRemovePrefix"] = route.Path.TrimEnd('/')
                    }
                ]
            });

            yarpClusters.Add(new ClusterConfig
            {
                ClusterId = clusterId,
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["primary"] = new DestinationConfig { Address = route.Destination }
                }
            });
        }

        return new FluxgateProxyConfig(yarpRoutes, yarpClusters);
    }
}

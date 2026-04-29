using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace Gateway.API.Proxy;

/// <summary>
/// Immutable snapshot of YARP routes + clusters, paired with a cancellable change token.
/// Calling SignalChange() causes YARP to call GetConfig() again.
/// </summary>
internal sealed class FluxgateProxyConfig : IProxyConfig
{
    private readonly CancellationTokenSource _cts = new();

    public FluxgateProxyConfig(
        IReadOnlyList<RouteConfig> routes,
        IReadOnlyList<ClusterConfig> clusters)
    {
        Routes = routes;
        Clusters = clusters;
        ChangeToken = new CancellationChangeToken(_cts.Token);
    }

    public IReadOnlyList<RouteConfig> Routes { get; }
    public IReadOnlyList<ClusterConfig> Clusters { get; }
    public IChangeToken ChangeToken { get; }

    /// <summary>Fires the change token so YARP will call GetConfig() for a fresh load.</summary>
    public void SignalChange() => _cts.Cancel();
}

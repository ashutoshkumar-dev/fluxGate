namespace Gateway.API.Proxy;

/// <summary>
/// Singleton notifier that signals YARP to reload its proxy config after a route CUD operation.
/// </summary>
public sealed class RouteChangeNotifier
{
    private volatile FluxgateProxyConfig? _currentConfig;

    internal void SetCurrentConfig(FluxgateProxyConfig config) => _currentConfig = config;

    /// <summary>Fires the change token on the current config, causing YARP to call GetConfig() again.</summary>
    public void NotifyChange() => _currentConfig?.SignalChange();
}

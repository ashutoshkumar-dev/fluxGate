using System.Security.Claims;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Gateway.API.Proxy;

/// <summary>
/// YARP ITransformProvider: injects gateway identification headers into every
/// proxied request (applied automatically to all routes).
///
///   X-Forwarded-User  — authenticated username (empty string for anonymous)
///   X-Request-Id      — ASP.NET Core TraceIdentifier (unique per request)
///   X-Gateway-Version — constant "fluxgate/1.0"
/// </summary>
public sealed class GatewayTransformProvider : ITransformProvider
{
    public void ValidateRoute(TransformRouteValidationContext context) { }

    public void ValidateCluster(TransformClusterValidationContext context) { }

    public void Apply(TransformBuilderContext context)
    {
        context.AddRequestTransform(transformContext =>
        {
            var httpCtx = transformContext.HttpContext;

            var user = httpCtx.User.FindFirst(ClaimTypes.Name)?.Value
                    ?? httpCtx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? string.Empty;

            transformContext.ProxyRequest.Headers.Remove("X-Forwarded-User");
            transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-User", user);

            transformContext.ProxyRequest.Headers.Remove("X-Request-Id");
            transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-Request-Id", httpCtx.TraceIdentifier);

            transformContext.ProxyRequest.Headers.Remove("X-Gateway-Version");
            transformContext.ProxyRequest.Headers.TryAddWithoutValidation("X-Gateway-Version", "fluxgate/1.0");

            return ValueTask.CompletedTask;
        });
    }
}

using System.Security.Claims;
using Gateway.Core.Interfaces;

namespace Gateway.API.Auth;

/// <summary>
/// Per-request middleware that enforces authRequired and roles from the matched DB route.
/// Must run after UseAuthentication() so that HttpContext.User is already populated.
///
/// Logic:
///   1. If no DB route matches the request path/method → skip (YARP will 404)
///   2. authRequired = false → allow through
///   3. authRequired = true, user not authenticated → 401
///   4. route.roles is non-empty and user has none of those roles → 403
///   5. Otherwise → allow through
/// </summary>
public sealed class RouteAuthorizationMiddleware
{
    private readonly RequestDelegate _next;

    public RouteAuthorizationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext ctx, IRouteRepository routeRepo)
    {
        var path = ctx.Request.Path.Value ?? string.Empty;
        var method = ctx.Request.Method;

        // Find the best matching active route (longest prefix match)
        var routes = (await routeRepo.GetAllAsync(isActive: true)).ToList();
        var matched = routes
            .Where(r =>
                path.StartsWith(r.Path, StringComparison.OrdinalIgnoreCase) &&
                (r.Method == "*" || r.Method.Equals(method, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(r => r.Path.Length)
            .FirstOrDefault();

        if (matched is null)
        {
            // No matching proxy route — let the pipeline continue (YARP will 404)
            await _next(ctx);
            return;
        }

        if (!matched.AuthRequired)
        {
            await _next(ctx);
            return;
        }

        // Route requires auth — check the principal
        if (ctx.User.Identity?.IsAuthenticated != true)
        {
            ctx.Response.StatusCode = 401;
            await ctx.Response.WriteAsJsonAsync(new { message = "Authentication required." });
            return;
        }

        // Role check — only enforced when the route specifies required roles
        if (matched.Roles.Count > 0)
        {
            var userRoles = ctx.User.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!matched.Roles.Any(r => userRoles.Contains(r)))
            {
                ctx.Response.StatusCode = 403;
                await ctx.Response.WriteAsJsonAsync(new { message = "Insufficient role." });
                return;
            }
        }

        await _next(ctx);
    }
}

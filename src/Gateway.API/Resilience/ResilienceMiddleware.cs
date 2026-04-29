using Gateway.Core.Interfaces;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Retry;

namespace Gateway.API.Resilience;

/// <summary>
/// Wraps downstream (YARP) requests in a per-route Polly v8 ResiliencePipeline.
/// Pipeline: Retry (max 3, exponential backoff 200ms base) → Circuit Breaker
///   (open after 5 failures in 30s, half-open after 60s).
///
/// IMPORTANT: YARP begins streaming the response body immediately, so we cannot
/// retry by re-invoking _next after YARP has started writing. Instead we:
///   1. Let YARP run once.
///   2. After _next returns, check the status code.
///   3. If 5xx and the pipeline says "should retry", we clear the response and
///      re-invoke _next — but only while the response body has not yet been sent
///      to the client (TestHost / buffered scenario).
///   4. Circuit breaker tracks failures independently.
///
/// In production YARP streams to the client immediately, so retries are only
/// safe when the downstream response has not yet started (e.g. connection errors
/// before upstream writes anything). This is the standard trade-off for a
/// streaming proxy.
/// </summary>
public sealed class ResilienceMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ResiliencePipelineRegistry<string> _registry;

    public ResilienceMiddleware(RequestDelegate next, ResiliencePipelineRegistry<string> registry)
    {
        _next     = next;
        _registry = registry;
    }

    public async Task InvokeAsync(HttpContext ctx, IRouteRepository routeRepo)
    {
        var path   = ctx.Request.Path.Value ?? "/";
        var method = ctx.Request.Method;

        // Only apply resilience to proxied routes
        var routes = await routeRepo.GetAllAsync(isActive: true, ctx.RequestAborted);
        var matched = routes
            .Where(r =>
                path.StartsWith(r.Path, StringComparison.OrdinalIgnoreCase) &&
                (r.Method == "*" || r.Method.Equals(method, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(r => r.Path.Length)
            .FirstOrDefault();

        if (matched is null)
        {
            await _next(ctx);
            return;
        }

        var pipeline = _registry.GetOrAddPipeline(
            matched.Id.ToString(),
            builder => ConfigurePipeline(builder));

        try
        {
            await pipeline.ExecuteAsync(async ct =>
            {
                // Only retry if the response has not started yet (safe to replace the body)
                if (ctx.Response.HasStarted)
                    return; // cannot retry once body is streaming

                await _next(ctx);

                // Surface 5xx as an exception so Polly can apply retry/CB logic
                if (ctx.Response.StatusCode >= 500 && !ctx.Response.HasStarted)
                    throw new HttpRequestException(
                        $"Upstream returned {ctx.Response.StatusCode}",
                        null,
                        (System.Net.HttpStatusCode)ctx.Response.StatusCode);
            }, ctx.RequestAborted);
        }
        catch (BrokenCircuitException)
        {
            if (!ctx.Response.HasStarted)
            {
                ctx.Response.StatusCode = 503;
                await ctx.Response.WriteAsJsonAsync(new
                {
                    message = "Service temporarily unavailable. Circuit breaker is open."
                });
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode is not null && (int)ex.StatusCode >= 500)
        {
            if (!ctx.Response.HasStarted)
            {
                ctx.Response.StatusCode = 502;
                await ctx.Response.WriteAsJsonAsync(new
                {
                    message = "Upstream service error after retries exhausted."
                });
            }
        }
    }

    private static void ConfigurePipeline(ResiliencePipelineBuilder builder)
    {
        // Inner: Circuit Breaker
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

        // Outer: Retry
        builder.AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType      = DelayBackoffType.Exponential,
            Delay            = TimeSpan.FromMilliseconds(200),
            ShouldHandle     = new PredicateBuilder()
                .Handle<HttpRequestException>(ex =>
                    ex.StatusCode is not null && (int)ex.StatusCode >= 500)
        });
    }
}

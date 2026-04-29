using FluentValidation;
using FluentValidation.AspNetCore;
using Gateway.API.Auth;
using Gateway.API.Metrics;
using Gateway.API.Middleware;
using Gateway.API.Proxy;
using Gateway.API.Resilience;
using Gateway.Core.Services;
using Gateway.Core.Validators;
using Gateway.Infrastructure.Observability;
using Gateway.Infrastructure.Persistence;
using Gateway.Infrastructure.Repositories;
using Gateway.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Polly;
using Polly.Registry;
using Serilog;
using StackExchange.Redis;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms.Builder;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, config) =>
        config.ReadFrom.Configuration(ctx.Configuration)
              .ReadFrom.Services(services));

    builder.Services.AddControllers();

    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssemblyContaining<RouteCreateDtoValidator>();

    var connStr = builder.Configuration.GetConnectionString("DefaultConnection");

    builder.Services.AddDbContext<GatewayDbContext>(options =>
        options.UseNpgsql(connStr));

    builder.Services.AddDbContext<AuthDbContext>(options =>
        options.UseNpgsql(connStr));

    builder.Services.AddScoped<Gateway.Core.Interfaces.IRouteRepository, RouteRepository>();
    builder.Services.AddScoped<Gateway.Core.Interfaces.IApiKeyRepository, ApiKeyRepository>();
    builder.Services.AddScoped<RouteService>();

    // ── Auth ──────────────────────────────────────────────────────────────
    var jwksUrl = builder.Configuration["Jwt:JwksUrl"]
        ?? "http://localhost:5100/.well-known/jwks.json";
    var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "fluxgate-auth";
    var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "fluxgate-gateway";

    builder.Services.AddMemoryCache();
    builder.Services.AddHttpClient("jwks");
    builder.Services.AddSingleton(sp =>
        new JwksService(sp.GetRequiredService<IHttpClientFactory>(),
                        sp.GetRequiredService<IMemoryCache>(),
                        jwksUrl));

    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultScheme          = "FluxgateMulti";
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddPolicyScheme("FluxgateMulti", "FluxgateMulti", options =>
        {
            // Route to ApiKey scheme when X-Api-Key is present; otherwise JWT Bearer
            options.ForwardDefaultSelector = ctx =>
                ctx.Request.Headers.ContainsKey("X-Api-Key")
                    ? ApiKeyAuthHandler.SchemeName
                    : JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidIssuer              = jwtIssuer,
                ValidateAudience         = true,
                ValidAudience            = jwtAudience,
                ValidateLifetime         = true,
                ValidateIssuerSigningKey = true,
            };
            // Populate signing keys from JwksService only when a token is present
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = async ctx =>
                {
                    var auth = ctx.Request.Headers.Authorization.ToString();
                    if (!auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        return;   // no token → skip JWKS fetch, JWT handler returns NoResult

                    var jwksSvc = ctx.HttpContext.RequestServices.GetRequiredService<JwksService>();
                    var keys = await jwksSvc.GetSigningKeysAsync();
                    ctx.Options.TokenValidationParameters.IssuerSigningKeys = keys;
                }
            };
        })
        .AddScheme<ApiKeyAuthOptions, ApiKeyAuthHandler>(ApiKeyAuthHandler.SchemeName, _ => { });

    builder.Services.AddAuthorization();

    // ── Observability ─────────────────────────────────────────────────────
    builder.Services.AddSingleton<MetricsRegistry>();
    builder.Services.AddHttpClient("seq");
    builder.Services.AddSingleton<Gateway.Core.Interfaces.ISeqLogsService, SeqLogsService>();

    // ── Redis ─────────────────────────────────────────────────────────────
    var redisConn = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    var redisCfg  = ConfigurationOptions.Parse(redisConn);
    redisCfg.AbortOnConnectFail = false;   // allow startup even if Redis is transiently unavailable
    builder.Services.AddSingleton<IConnectionMultiplexer>(
        ConnectionMultiplexer.Connect(redisCfg));

    // YARP dynamic proxy — config is loaded from the routes table
    builder.Services.AddSingleton<RouteChangeNotifier>();
    builder.Services.AddSingleton<IProxyConfigProvider, DatabaseProxyConfigProvider>();
    builder.Services.AddSingleton<ITransformProvider, GatewayTransformProvider>();
    builder.Services.AddReverseProxy();

    // ── Polly resilience registry ─────────────────────────────────────────
    builder.Services.AddSingleton(new ResiliencePipelineRegistry<string>());

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseMiddleware<MetricsMiddleware>();

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<RouteAuthorizationMiddleware>();
    app.UseMiddleware<RateLimitMiddleware>();
    app.UseMiddleware<ResponseCacheMiddleware>();
    app.UseMiddleware<ResilienceMiddleware>();

    // Controllers handle /health, /gateway/routes etc. — registered first so they take priority
    app.MapControllers();

    // YARP handles everything else — proxies to upstream based on DB routes
    app.MapReverseProxy();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Gateway.API failed to start");
}
finally
{
    Log.CloseAndFlush();
}

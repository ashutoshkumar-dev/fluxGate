using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using FluentAssertions;
using Gateway.API.Auth;
using Gateway.Core.Domain.Entities;
using Gateway.Core.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace Gateway.Tests.Auth;

// ── ApiKeyAuthHandler ────────────────────────────────────────────────────────

public class ApiKeyAuthHandlerTests
{
    private static async Task<AuthenticateResult> RunAsync(
        string? headerValue,
        ApiKey? foundKey)
    {
        var repoMock = new Mock<IApiKeyRepository>();

        if (headerValue is not null)
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(headerValue)))
                              .ToLowerInvariant();
            repoMock.Setup(r => r.GetByKeyHashAsync(hash, default)).ReturnsAsync(foundKey);
        }

        var options     = new ApiKeyAuthOptions();
        var optMon      = Mock.Of<IOptionsMonitor<ApiKeyAuthOptions>>(m => m.Get(It.IsAny<string>()) == options);
        var logger      = new LoggerFactory();
        var encoder     = UrlEncoder.Default;
        var handler     = new ApiKeyAuthHandler(optMon, logger, encoder, repoMock.Object);

        var ctx = new DefaultHttpContext();
        await handler.InitializeAsync(new AuthenticationScheme(
            ApiKeyAuthHandler.SchemeName, null, typeof(ApiKeyAuthHandler)), ctx);

        if (headerValue is not null)
            ctx.Request.Headers["X-Api-Key"] = headerValue;

        return await handler.AuthenticateAsync();
    }

    [Fact]
    public async Task ValidActiveKey_ReturnsSuccess()
    {
        var key = new ApiKey
        {
            Id = Guid.NewGuid(), KeyHash = "x", OwnerService = "billing",
            Scopes = ["read"], IsActive = true, CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await RunAsync("raw-key-value", key);

        result.Succeeded.Should().BeTrue();
        result.Principal!.Identity!.Name.Should().Be("billing");
    }

    [Fact]
    public async Task RevokedKey_ReturnsFail()
    {
        var key = new ApiKey
        {
            Id = Guid.NewGuid(), KeyHash = "x", OwnerService = "billing",
            Scopes = [], IsActive = false, CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await RunAsync("raw-key", key);

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("revoked");
    }

    [Fact]
    public async Task ExpiredKey_ReturnsFail()
    {
        var key = new ApiKey
        {
            Id = Guid.NewGuid(), KeyHash = "x", OwnerService = "billing",
            Scopes = [], IsActive = true, CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1)  // expired
        };

        var result = await RunAsync("raw-key", key);

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("expired");
    }

    [Fact]
    public async Task MissingHeader_ReturnsNoResult()
    {
        var result = await RunAsync(headerValue: null, foundKey: null);
        result.None.Should().BeTrue("no X-Api-Key header → pass to next scheme");
    }

    [Fact]
    public async Task UnknownKey_ReturnsFail()
    {
        var result = await RunAsync("unknown-key", foundKey: null);
        result.Succeeded.Should().BeFalse();
    }
}

// ── RouteAuthorizationMiddleware ─────────────────────────────────────────────

public class RouteAuthorizationMiddlewareTests
{
    private static (RouteAuthorizationMiddleware mw, Mock<IRouteRepository> repo) Build(
        RequestDelegate next)
    {
        var repo = new Mock<IRouteRepository>();
        return (new RouteAuthorizationMiddleware(next), repo);
    }

    private static Route MakeRoute(string path, string method, bool authRequired, string[] roles) =>
        new() { Id = Guid.NewGuid(), Path = path, Method = method, Destination = "http://up",
                AuthRequired = authRequired, Roles = [..roles], IsActive = true };

    private static ClaimsPrincipal MakePrincipal(string role)
    {
        var claims = new[] { new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.Name, "alice") };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public async Task NoMatchingRoute_CallsNext()
    {
        var called = false;
        var (mw, repo) = Build(_ => { called = true; return Task.CompletedTask; });
        repo.Setup(r => r.GetAllAsync(true, default)).ReturnsAsync([]);

        var ctx = new DefaultHttpContext();
        ctx.Request.Path   = "/no-match";
        ctx.Request.Method = "GET";

        await mw.InvokeAsync(ctx, repo.Object);
        called.Should().BeTrue();
    }

    [Fact]
    public async Task AuthNotRequired_CallsNext()
    {
        var called = false;
        var (mw, repo) = Build(_ => { called = true; return Task.CompletedTask; });
        repo.Setup(r => r.GetAllAsync(true, default))
            .ReturnsAsync([MakeRoute("/public", "GET", authRequired: false, [])]);

        var ctx = new DefaultHttpContext();
        ctx.Request.Path   = "/public/data";
        ctx.Request.Method = "GET";

        await mw.InvokeAsync(ctx, repo.Object);
        called.Should().BeTrue();
    }

    [Fact]
    public async Task AuthRequired_NoToken_Returns401()
    {
        var (mw, repo) = Build(_ => Task.CompletedTask);
        repo.Setup(r => r.GetAllAsync(true, default))
            .ReturnsAsync([MakeRoute("/secure", "GET", authRequired: true, [])]);

        var ctx = new DefaultHttpContext();
        ctx.Request.Path   = "/secure/data";
        ctx.Request.Method = "GET";
        ctx.Response.Body  = new MemoryStream();

        await mw.InvokeAsync(ctx, repo.Object);
        ctx.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task AuthRequired_WrongRole_Returns403()
    {
        var (mw, repo) = Build(_ => Task.CompletedTask);
        repo.Setup(r => r.GetAllAsync(true, default))
            .ReturnsAsync([MakeRoute("/admin", "GET", authRequired: true, ["admin"])]);

        var ctx = new DefaultHttpContext();
        ctx.Request.Path   = "/admin/data";
        ctx.Request.Method = "GET";
        ctx.Response.Body  = new MemoryStream();
        ctx.User           = MakePrincipal("user");  // has 'user' role, not 'admin'

        await mw.InvokeAsync(ctx, repo.Object);
        ctx.Response.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task AuthRequired_CorrectRole_CallsNext()
    {
        var called = false;
        var (mw, repo) = Build(_ => { called = true; return Task.CompletedTask; });
        repo.Setup(r => r.GetAllAsync(true, default))
            .ReturnsAsync([MakeRoute("/admin", "GET", authRequired: true, ["admin"])]);

        var ctx = new DefaultHttpContext();
        ctx.Request.Path   = "/admin/data";
        ctx.Request.Method = "GET";
        ctx.User           = MakePrincipal("admin");

        await mw.InvokeAsync(ctx, repo.Object);
        called.Should().BeTrue();
    }

    [Fact]
    public async Task AuthRequired_NoRoles_AnyAuthenticatedUser_CallsNext()
    {
        var called = false;
        var (mw, repo) = Build(_ => { called = true; return Task.CompletedTask; });
        repo.Setup(r => r.GetAllAsync(true, default))
            .ReturnsAsync([MakeRoute("/private", "GET", authRequired: true, [])]);

        var ctx = new DefaultHttpContext();
        ctx.Request.Path   = "/private/x";
        ctx.Request.Method = "GET";
        ctx.User           = MakePrincipal("user");  // authenticated, any role OK

        await mw.InvokeAsync(ctx, repo.Object);
        called.Should().BeTrue();
    }
}

using FluentAssertions;
using Gateway.API.Controllers;
using Gateway.API.Proxy;
using Gateway.Core.DTOs;
using Gateway.Core.Services;
using Gateway.Core.Interfaces;
using Gateway.Core.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Gateway.Tests.Controllers;

/// <summary>
/// Verifies that RoutesController calls RouteChangeNotifier.NotifyChange()
/// after every CUD operation so YARP hot-reloads its config.
/// </summary>
public class RoutesControllerTests
{
    private static (RoutesController ctrl, RouteChangeNotifier notifier, Mock<IRouteRepository> repo)
        Build()
    {
        var repoMock = new Mock<IRouteRepository>();
        var service = new RouteService(repoMock.Object);
        var notifier = new RouteChangeNotifier();
        var ctrl = new RoutesController(service, notifier);
        return (ctrl, notifier, repoMock);
    }

    private static RouteCreateDto ValidCreateDto(string path = "/api/test") => new()
    {
        Path = path,
        Method = "GET",
        Destination = "http://upstream/",
        IsActive = true,
        Roles = []
    };

    private static RouteUpdateDto ValidUpdateDto(string path = "/api/test") => new()
    {
        Path = path,
        Method = "GET",
        Destination = "http://upstream/",
        IsActive = true,
        Roles = []
    };

    // ── Create ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_CallsNotifyChange()
    {
        var (ctrl, notifier, repo) = Build();
        repo.Setup(r => r.AddAsync(It.IsAny<Route>(), default))
            .ReturnsAsync((Route r, CancellationToken _) => r);

        // Prime a config snapshot so we can observe the token fire
        var notifierSvc = BuildScopeNotifier(notifier);

        var result = await ctrl.Create(ValidCreateDto(), default);

        result.Should().BeOfType<CreatedAtActionResult>();
        notifierSvc.ChangeTokenFired.Should().BeTrue("Create must trigger YARP hot-reload");
    }

    [Fact]
    public async Task Update_WhenRouteExists_CallsNotifyChange()
    {
        var (ctrl, notifier, repo) = Build();
        var id = Guid.NewGuid();
        var route = new Route
        {
            Id = id, Path = "/api/test", Method = "GET",
            Destination = "http://up/", IsActive = true, Roles = []
        };
        repo.Setup(r => r.GetByIdAsync(id, default)).ReturnsAsync(route);
        repo.Setup(r => r.UpdateAsync(It.IsAny<Route>(), default))
            .ReturnsAsync((Route r, CancellationToken _) => r);

        var notifierSvc = BuildScopeNotifier(notifier);

        var result = await ctrl.Update(id, ValidUpdateDto(), default);

        result.Should().BeOfType<OkObjectResult>();
        notifierSvc.ChangeTokenFired.Should().BeTrue("Update must trigger YARP hot-reload");
    }

    [Fact]
    public async Task Update_WhenRouteNotFound_DoesNotCallNotifyChange()
    {
        var (ctrl, notifier, repo) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Route?)null);

        var notifierSvc = BuildScopeNotifier(notifier);

        var result = await ctrl.Update(Guid.NewGuid(), ValidUpdateDto(), default);

        result.Should().BeOfType<NotFoundResult>();
        notifierSvc.ChangeTokenFired.Should().BeFalse("NotifyChange must NOT fire on 404");
    }

    [Fact]
    public async Task Delete_WhenRouteExists_CallsNotifyChange()
    {
        var (ctrl, notifier, repo) = Build();
        var id = Guid.NewGuid();
        repo.Setup(r => r.DeleteAsync(id, default)).ReturnsAsync(true);

        var notifierSvc = BuildScopeNotifier(notifier);

        var result = await ctrl.Delete(id, default);

        result.Should().BeOfType<NoContentResult>();
        notifierSvc.ChangeTokenFired.Should().BeTrue("Delete must trigger YARP hot-reload");
    }

    [Fact]
    public async Task Delete_WhenRouteNotFound_DoesNotCallNotifyChange()
    {
        var (ctrl, notifier, repo) = Build();
        repo.Setup(r => r.DeleteAsync(It.IsAny<Guid>(), default)).ReturnsAsync(false);

        var notifierSvc = BuildScopeNotifier(notifier);

        var result = await ctrl.Delete(Guid.NewGuid(), default);

        result.Should().BeOfType<NotFoundResult>();
        notifierSvc.ChangeTokenFired.Should().BeFalse("NotifyChange must NOT fire on 404");
    }

    // ── Helper: wraps RouteChangeNotifier with change token observation ──

    private static NotifierObserver BuildScopeNotifier(RouteChangeNotifier notifier)
    {
        // Inject a fake config so the notifier has a token to fire
        var fakeConfig = new FluxgateProxyConfigAccessor();
        notifier.SetCurrentConfig(fakeConfig.Config);
        return new NotifierObserver(fakeConfig.Config);
    }
}

/// <summary>
/// Grants test access to FluxgateProxyConfig internal members for observation.
/// </summary>
internal sealed class FluxgateProxyConfigAccessor
{
    public FluxgateProxyConfig Config { get; } =
        new FluxgateProxyConfig([], []);
}

internal sealed class NotifierObserver
{
    private readonly Yarp.ReverseProxy.Configuration.IProxyConfig _config;
    public NotifierObserver(Yarp.ReverseProxy.Configuration.IProxyConfig config) => _config = config;
    public bool ChangeTokenFired => _config.ChangeToken.HasChanged;
}

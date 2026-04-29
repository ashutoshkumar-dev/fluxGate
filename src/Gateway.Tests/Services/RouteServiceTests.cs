using FluentAssertions;
using Gateway.Core.Domain.Entities;
using Gateway.Core.DTOs;
using Gateway.Core.Interfaces;
using Gateway.Core.Services;
using Moq;

namespace Gateway.Tests.Services;

public class RouteServiceTests
{
    private readonly Mock<IRouteRepository> _repoMock = new();
    private readonly RouteService _sut;

    public RouteServiceTests()
    {
        _sut = new RouteService(_repoMock.Object);
    }

    // ── CreateAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ValidDto_ReturnsMappedRouteDto()
    {
        var dto = new RouteCreateDto
        {
            Path = "/orders",
            Method = "get",
            Destination = "http://order-service/api/orders",
            AuthRequired = true,
            Roles = ["admin"],
            IsActive = true
        };

        _repoMock.Setup(r => r.AddAsync(It.IsAny<Route>(), default))
            .ReturnsAsync((Route r, CancellationToken _) => r);

        var result = await _sut.CreateAsync(dto);

        result.Should().NotBeNull();
        result.Path.Should().Be("/orders");
        result.Method.Should().Be("GET"); // normalized to uppercase
        result.Destination.Should().Be("http://order-service/api/orders");
        result.IsActive.Should().BeTrue();
        result.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateAsync_NormalizesMethodToUpperCase()
    {
        var dto = new RouteCreateDto { Path = "/test", Method = "post", Destination = "http://svc/test" };

        _repoMock.Setup(r => r.AddAsync(It.IsAny<Route>(), default))
            .ReturnsAsync((Route r, CancellationToken _) => r);

        var result = await _sut.CreateAsync(dto);

        result.Method.Should().Be("POST");
    }

    [Fact]
    public async Task CreateAsync_WithRateLimit_MapsRateLimitCorrectly()
    {
        var dto = new RouteCreateDto
        {
            Path = "/limited",
            Method = "GET",
            Destination = "http://svc/limited",
            RateLimit = new RateLimitDto { Limit = 100, WindowSeconds = 60 }
        };

        _repoMock.Setup(r => r.AddAsync(It.IsAny<Route>(), default))
            .ReturnsAsync((Route r, CancellationToken _) => r);

        var result = await _sut.CreateAsync(dto);

        result.RateLimit.Should().NotBeNull();
        result.RateLimit!.Limit.Should().Be(100);
        result.RateLimit.WindowSeconds.Should().Be(60);
    }

    // ── GetAllAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsAllRoutes_WhenNoFilter()
    {
        var routes = new List<Route>
        {
            new() { Id = Guid.NewGuid(), Path = "/a", Method = "GET", Destination = "http://a", IsActive = true },
            new() { Id = Guid.NewGuid(), Path = "/b", Method = "POST", Destination = "http://b", IsActive = false }
        };

        _repoMock.Setup(r => r.GetAllAsync(null, default)).ReturnsAsync(routes);

        var result = await _sut.GetAllAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_PassesIsActiveFilterToRepository()
    {
        _repoMock.Setup(r => r.GetAllAsync(true, default)).ReturnsAsync([]);

        await _sut.GetAllAsync(isActive: true);

        _repoMock.Verify(r => r.GetAllAsync(true, default), Times.Once);
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ExistingId_ReturnsUpdatedDto()
    {
        var id = Guid.NewGuid();
        var existing = new Route { Id = id, Path = "/old", Method = "GET", Destination = "http://old" };
        var dto = new RouteUpdateDto { Path = "/new", Method = "POST", Destination = "http://new", IsActive = false };

        _repoMock.Setup(r => r.GetByIdAsync(id, default)).ReturnsAsync(existing);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<Route>(), default))
            .ReturnsAsync((Route r, CancellationToken _) => r);

        var result = await _sut.UpdateAsync(id, dto);

        result.Should().NotBeNull();
        result!.Path.Should().Be("/new");
        result.Method.Should().Be("POST");
        result.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ReturnsNull()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Route?)null);

        var result = await _sut.UpdateAsync(Guid.NewGuid(), new RouteUpdateDto
        {
            Path = "/x",
            Method = "GET",
            Destination = "http://x"
        });

        result.Should().BeNull();
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_ExistingId_ReturnsTrue()
    {
        var id = Guid.NewGuid();
        _repoMock.Setup(r => r.DeleteAsync(id, default)).ReturnsAsync(true);

        var result = await _sut.DeleteAsync(id);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_ReturnsFalse()
    {
        _repoMock.Setup(r => r.DeleteAsync(It.IsAny<Guid>(), default)).ReturnsAsync(false);

        var result = await _sut.DeleteAsync(Guid.NewGuid());

        result.Should().BeFalse();
    }
}

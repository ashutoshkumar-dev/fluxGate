using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Gateway.AuthService.Security;
using Gateway.AuthService.Services;
using Gateway.Core.Domain.Entities;
using Gateway.Core.DTOs.Auth;
using Gateway.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Gateway.Tests.Auth;

public class UserServiceTests
{
    private static (UserService service, Mock<IUserRepository> repoMock) Build()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["Jwt:AccessTokenExpiryMinutes"] = "60"
            })
            .Build();

        var rsaProvider = new RsaKeyProvider();
        var tokenService = new TokenService(rsaProvider, config);
        var repoMock = new Mock<IUserRepository>();
        var service = new UserService(repoMock.Object, tokenService);
        return (service, repoMock);
    }

    [Fact]
    public async Task RegisterAsync_NewUser_ReturnsSuccessDto()
    {
        var (service, repoMock) = Build();
        repoMock.Setup(r => r.ExistsAsync("alice", "alice@example.com", default))
                .ReturnsAsync(false);
        repoMock.Setup(r => r.AddAsync(It.IsAny<User>(), default))
                .ReturnsAsync((User u, CancellationToken _) => u);

        var dto = new RegisterDto { Username = "alice", Email = "alice@example.com", Password = "Secret123" };
        var (result, error) = await service.RegisterAsync(dto);

        error.Should().BeNull();
        result.Should().NotBeNull();
        result!.Username.Should().Be("alice");
        result.Email.Should().Be("alice@example.com");
        result.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RegisterAsync_DuplicateUser_ReturnsError()
    {
        var (service, repoMock) = Build();
        repoMock.Setup(r => r.ExistsAsync("alice", "alice@example.com", default))
                .ReturnsAsync(true);

        var dto = new RegisterDto { Username = "alice", Email = "alice@example.com", Password = "Secret123" };
        var (result, error) = await service.RegisterAsync(dto);

        result.Should().BeNull();
        error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsToken()
    {
        var (service, repoMock) = Build();
        var hash = BCrypt.Net.BCrypt.HashPassword("Secret123");
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "alice",
            Email = "alice@example.com",
            PasswordHash = hash,
            Roles = ["user"]
        };
        repoMock.Setup(r => r.GetByUsernameAsync("alice", default)).ReturnsAsync(user);

        var result = await service.LoginAsync(new LoginDto { Username = "alice", Password = "Secret123" });

        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();
        result.ExpiresIn.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsNull()
    {
        var (service, repoMock) = Build();
        var hash = BCrypt.Net.BCrypt.HashPassword("RightPassword");
        var user = new User
        {
            Id = Guid.NewGuid(), Username = "alice", Email = "a@a.com",
            PasswordHash = hash, Roles = ["user"]
        };
        repoMock.Setup(r => r.GetByUsernameAsync("alice", default)).ReturnsAsync(user);

        var result = await service.LoginAsync(new LoginDto { Username = "alice", Password = "WrongPassword" });

        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_UnknownUser_ReturnsNull()
    {
        var (service, repoMock) = Build();
        repoMock.Setup(r => r.GetByUsernameAsync("ghost", default)).ReturnsAsync((User?)null);

        var result = await service.LoginAsync(new LoginDto { Username = "ghost", Password = "Secret123" });

        result.Should().BeNull();
    }
}

public class TokenServiceTests
{
    private static (TokenService tokenService, RsaKeyProvider keyProvider) Build()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["Jwt:AccessTokenExpiryMinutes"] = "30"
            })
            .Build();
        var provider = new RsaKeyProvider();
        return (new TokenService(provider, config), provider);
    }

    [Fact]
    public void CreateToken_ContainsRequiredClaims()
    {
        var (tokenService, _) = Build();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "bob",
            Email = "bob@example.com",
            PasswordHash = "hash",
            Roles = ["user", "admin"]
        };

        var rawToken = tokenService.CreateToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(rawToken);

        jwt.Subject.Should().Be(user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "email" && c.Value == user.Email);
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "user");
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "admin");
        jwt.Issuer.Should().Be("test-issuer");
    }

    [Fact]
    public void BuildJwks_ContainsRsaPublicKey_WithCorrectFields()
    {
        var (tokenService, provider) = Build();

        var jwks = tokenService.BuildJwks();

        // Inspect via anonymous type using reflection / JSON
        var json = System.Text.Json.JsonSerializer.Serialize(jwks);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var keys = doc.RootElement.GetProperty("keys");

        keys.GetArrayLength().Should().Be(1);
        var key = keys[0];
        key.GetProperty("kty").GetString().Should().Be("RSA");
        key.GetProperty("alg").GetString().Should().Be("RS256");
        key.GetProperty("use").GetString().Should().Be("sig");
        key.GetProperty("kid").GetString().Should().Be(provider.KeyId);
        key.GetProperty("n").GetString().Should().NotBeNullOrEmpty();
        key.GetProperty("e").GetString().Should().NotBeNullOrEmpty();
    }
}

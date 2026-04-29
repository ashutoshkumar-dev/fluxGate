using Gateway.AuthService.Security;
using Gateway.Core.Domain.Entities;
using Gateway.Core.DTOs.Auth;
using Gateway.Core.Interfaces;

namespace Gateway.AuthService.Services;

public class UserService
{
    private readonly IUserRepository _userRepo;
    private readonly TokenService _tokenService;

    public UserService(IUserRepository userRepo, TokenService tokenService)
    {
        _userRepo = userRepo;
        _tokenService = tokenService;
    }

    public async Task<(RegisterResponseDto? result, string? error)> RegisterAsync(
        RegisterDto dto, CancellationToken ct = default)
    {
        if (await _userRepo.ExistsAsync(dto.Username, dto.Email, ct))
            return (null, "Username or email already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = dto.Username,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Roles = ["user"],
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _userRepo.AddAsync(user, ct);

        return (new RegisterResponseDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email
        }, null);
    }

    public async Task<LoginResponseDto?> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        var user = await _userRepo.GetByUsernameAsync(dto.Username, ct);
        if (user is null) return null;

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return null;

        var token = _tokenService.CreateToken(user);

        return new LoginResponseDto
        {
            AccessToken = token,
            ExpiresIn = 3600
        };
    }
}

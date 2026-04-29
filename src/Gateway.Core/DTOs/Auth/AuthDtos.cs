namespace Gateway.Core.DTOs.Auth;

public class RegisterDto
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterResponseDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class LoginDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
}

public class CreateApiKeyDto
{
    public string OwnerService { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = [];
}

public class CreateApiKeyResponseDto
{
    public Guid Id { get; set; }
    public string RawKey { get; set; } = string.Empty;
}

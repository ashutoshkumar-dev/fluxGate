namespace Gateway.Core.DTOs;

public class RouteDto
{
    public Guid Id { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public bool AuthRequired { get; set; }
    public List<string> Roles { get; set; } = [];
    public RateLimitDto? RateLimit { get; set; }
    public int? CacheTtlSeconds { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

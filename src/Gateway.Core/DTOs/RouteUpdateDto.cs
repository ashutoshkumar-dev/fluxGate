namespace Gateway.Core.DTOs;

public class RouteUpdateDto
{
    public string Path { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public bool AuthRequired { get; set; } = true;
    public List<string> Roles { get; set; } = [];
    public RateLimitDto? RateLimit { get; set; }
    public int? CacheTtlSeconds { get; set; }
    public bool IsActive { get; set; } = true;
}

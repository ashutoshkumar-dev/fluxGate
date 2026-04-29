namespace Gateway.Core.DTOs;

public class RateLimitDto
{
    public int Limit { get; set; }
    public int WindowSeconds { get; set; }
}

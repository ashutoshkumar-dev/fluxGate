namespace Gateway.Core.Domain.Entities;

public class RateLimitConfig
{
    public int Limit { get; set; }
    public int WindowSeconds { get; set; }
}

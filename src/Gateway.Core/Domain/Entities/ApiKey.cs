namespace Gateway.Core.Domain.Entities;

public class ApiKey
{
    public Guid Id { get; set; }
    public string KeyHash { get; set; } = string.Empty;
    public string OwnerService { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}

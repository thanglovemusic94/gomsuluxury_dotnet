namespace WebShop.Models;

public class AuditLog
{
    public long Id { get; set; }

    public int? UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public int EntityId { get; set; }

    public string? EntityTitle { get; set; }

    public string? Summary { get; set; }

    public string? Details { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

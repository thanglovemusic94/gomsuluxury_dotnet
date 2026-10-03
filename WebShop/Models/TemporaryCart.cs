namespace WebShop.Models;

public class TemporaryCart
{
    public int Id { get; set; }

    public string Token { get; set; } = string.Empty;

    public int ProductId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;

    public decimal UnitPrice { get; set; }

    public string? CustomerPhone { get; set; }

    public string? FbUserId { get; set; }

    public string Status { get; set; } = TemporaryCartStatuses.Pending;

    public bool IsDemo { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? OrderId { get; set; }

    public Product Product { get; set; } = null!;

    public Order? Order { get; set; }
}

public static class TemporaryCartStatuses
{
    public const string Pending = "Pending";
    public const string Ordered = "Ordered";
    public const string Expired = "Expired";
}

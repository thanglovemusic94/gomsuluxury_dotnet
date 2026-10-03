namespace WebShop.Models;

public class ProductReview
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public int? UserId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public int Rating { get; set; }

    public string Comment { get; set; } = string.Empty;

    public bool IsApproved { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Product Product { get; set; } = null!;

    public User? User { get; set; }
}

namespace WebShop.Models;

public class Order
{
    public int Id { get; set; }

    public string OrderCode { get; set; } = string.Empty;

    public int? UserId { get; set; }

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = "Pending";

    public string PaymentMethod { get; set; } = string.Empty;

    public string PaymentStatus { get; set; } = "Unpaid";

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerPhone { get; set; } = string.Empty;

    public string ShippingAddress { get; set; } = string.Empty;

    public string? OrderNote { get; set; }

    /// <summary>Nguồn đơn, vd. LP:am-tra-hoa-bien hoặc Web.</summary>
    public string? Source { get; set; }

    public User? User { get; set; }

    public List<OrderItem> Items { get; set; } = [];
}

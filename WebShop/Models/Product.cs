namespace WebShop.Models;

public class Product : ISoftDeletable
{
    public int Id { get; set; }

    public int CategoryId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public decimal? DiscountPrice { get; set; }

    public decimal CostPrice { get; set; }

    public int Stock { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? ShortDescription { get; set; }

    public string ImageUrl { get; set; } = string.Empty;

    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Ẩn giá trên shop, hiện CTA gọi / Zalo / Facebook (vẫn giữ giá trong Admin).
    /// </summary>
    public bool HidePrice { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? MetaTitle { get; set; }

    public string? MetaDescription { get; set; }

    public string? SeoImage { get; set; }

    public string? SeoFocusKeyword { get; set; }

    public int SeoScore { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    public Category Category { get; set; } = null!;

    public List<ProductCategory> ProductCategories { get; set; } = [];

    public List<ProductImage> Images { get; set; } = [];

    /// <summary>Ảnh gallery khác ảnh chính — chỉ dùng hover card, không lưu DB.</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? CardHoverImageUrl { get; set; }

    public List<ProductReview> Reviews { get; set; } = [];

    public List<OrderItem> OrderItems { get; set; } = [];
}

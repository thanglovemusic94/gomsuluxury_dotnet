namespace WebShop.Models;

public class CustomPage : ISoftDeletable
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public bool IsPublished { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? MetaTitle { get; set; }

    public string? MetaDescription { get; set; }

    public string? SeoImage { get; set; }

    public string? SeoFocusKeyword { get; set; }

    public int SeoScore { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }
}

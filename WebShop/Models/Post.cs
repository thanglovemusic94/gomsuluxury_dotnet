namespace WebShop.Models;

public class Post : ISoftDeletable
{
    public int Id { get; set; }

    public int CategoryId { get; set; }

    public int AuthorId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public string Content { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public string? MetaTitle { get; set; }

    public string? MetaDescription { get; set; }

    public string? SeoImage { get; set; }

    public string? SeoFocusKeyword { get; set; }

    public int SeoScore { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    public Category Category { get; set; } = null!;

    public User Author { get; set; } = null!;
}

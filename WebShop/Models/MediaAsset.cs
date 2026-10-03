namespace WebShop.Models;

public class MediaAsset : ISoftDeletable
{
    public int Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string? Alt { get; set; }

    public string Folder { get; set; } = "khac";

    public bool IsImage { get; set; }

    public string OriginalPath { get; set; } = string.Empty;

    public string OriginalUrl { get; set; } = string.Empty;

    public long OriginalBytes { get; set; }

    /// <summary>SHA-256 hex of original file bytes (for upload de-duplication).</summary>
    public string? ContentHash { get; set; }

    public string? ThumbPath { get; set; }

    public string? ThumbUrl { get; set; }

    public string? MediumPath { get; set; }

    public string? MediumUrl { get; set; }

    public string? LargePath { get; set; }

    public string? LargeUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }
}

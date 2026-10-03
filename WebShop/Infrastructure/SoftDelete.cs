using WebShop.Models;

namespace WebShop.Infrastructure;

public static class SoftDelete
{
    public static void Mark(ISoftDeletable entity, string? username)
    {
        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        entity.DeletedBy = string.IsNullOrWhiteSpace(username) ? "unknown" : username.Trim();
    }

    public static void Restore(ISoftDeletable entity)
    {
        entity.IsDeleted = false;
        entity.DeletedAt = null;
        entity.DeletedBy = null;
    }

    public static void MarkSlugDeleted(ref string slug, int id)
    {
        var suffix = $"-del-{id}";
        if (!slug.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            slug = $"{slug}{suffix}";
    }

    public static void RestoreSlug(ref string slug, int id)
    {
        var suffix = $"-del-{id}";
        if (slug.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            slug = slug[..^suffix.Length];
    }
}

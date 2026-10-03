using Microsoft.EntityFrameworkCore;
using WebShop.Data;

namespace WebShop.Infrastructure;

/// <summary>Soft/hard delete cho nội dung có thùng rác (sản phẩm, bài viết, trang tĩnh).</summary>
public static class AdminContentDelete
{
    public const string ModeTrash = "trash";
    public const string ModeHard = "hard";

    public static bool IsHard(string? mode) =>
        string.Equals(mode, ModeHard, StringComparison.OrdinalIgnoreCase);

    public static async Task<(int Count, string? Error)> DeletePostsAsync(
        AppDbContext db, AuditService audit, IEnumerable<int> ids, string? mode, string? username)
    {
        var idList = NormalizeIds(ids);
        if (idList.Count == 0)
            return (0, null);

        var hard = IsHard(mode);
        var items = await db.Posts.Where(item => idList.Contains(item.Id)).ToListAsync();
        if (items.Count == 0)
            return (0, null);

        if (hard)
        {
            var snapshots = items.Select(post => (
                post.Id,
                post.Title,
                Details: $"Title: {post.Title}\nSlug: {post.Slug}\nCategoryId: {post.CategoryId}\nAuthorId: {post.AuthorId}\nImageUrl: {post.ImageUrl}\nSummary: {post.Summary}"
            )).ToList();
            db.Posts.RemoveRange(items);
            var error = await DbSave.TryDeleteAsync(db);
            if (error is not null)
                return (0, error);
            foreach (var row in snapshots)
                await audit.LogAsync(AuditActions.HardDelete, AuditEntities.Post, row.Id, row.Title, "Xóa vĩnh viễn", row.Details);
            return (snapshots.Count, null);
        }

        foreach (var post in items)
        {
            SoftDelete.Mark(post, username);
            var slug = post.Slug;
            SoftDelete.MarkSlugDeleted(ref slug, post.Id);
            post.Slug = slug;
            post.IsPublished = false;
        }

        await db.SaveChangesAsync();
        foreach (var post in items)
        {
            await audit.LogAsync(AuditActions.SoftDelete, AuditEntities.Post, post.Id, post.Title, "Đưa vào thùng rác",
                $"Title: {post.Title}\nSlug: {post.Slug}");
        }

        return (items.Count, null);
    }

    public static async Task<(int Count, string? Error)> DeleteProductsAsync(
        AppDbContext db, AuditService audit, IEnumerable<int> ids, string? mode, string? username)
    {
        var idList = NormalizeIds(ids);
        if (idList.Count == 0)
            return (0, null);

        var hard = IsHard(mode);
        var items = await db.Products.Where(item => idList.Contains(item.Id)).ToListAsync();
        if (items.Count == 0)
            return (0, null);

        if (hard)
        {
            var snapshots = items.Select(product => (
                product.Id,
                product.Name,
                Details: $"Name: {product.Name}\nSlug: {product.Slug}\nPrice: {product.Price:0.##}\nStock: {product.Stock}\nImageUrl: {product.ImageUrl}\nCategoryId: {product.CategoryId}"
            )).ToList();
            db.Products.RemoveRange(items);
            var error = await DbSave.TryDeleteAsync(db);
            if (error is not null)
                return (0, error);
            foreach (var row in snapshots)
                await audit.LogAsync(AuditActions.HardDelete, AuditEntities.Product, row.Id, row.Name, "Xóa vĩnh viễn", row.Details);
            return (snapshots.Count, null);
        }

        foreach (var product in items)
        {
            SoftDelete.Mark(product, username);
            var slug = product.Slug;
            SoftDelete.MarkSlugDeleted(ref slug, product.Id);
            product.Slug = slug;
            product.IsVisible = false;
        }

        await db.SaveChangesAsync();
        foreach (var product in items)
        {
            await audit.LogAsync(AuditActions.SoftDelete, AuditEntities.Product, product.Id, product.Name, "Đưa vào thùng rác",
                $"Name: {product.Name}\nSlug: {product.Slug}");
        }

        return (items.Count, null);
    }

    public static async Task<(int Count, string? Error)> DeletePagesAsync(
        AppDbContext db, AuditService audit, IEnumerable<int> ids, string? mode, string? username)
    {
        var idList = NormalizeIds(ids);
        if (idList.Count == 0)
            return (0, null);

        var hard = IsHard(mode);
        var items = await db.CustomPages.Where(item => idList.Contains(item.Id)).ToListAsync();
        if (items.Count == 0)
            return (0, null);

        if (hard)
        {
            var snapshots = items.Select(page => (
                page.Id,
                page.Title,
                Details: $"Title: {page.Title}\nSlug: {page.Slug}\nIsPublished: {page.IsPublished}\nContentLength: {page.Content?.Length ?? 0}"
            )).ToList();
            db.CustomPages.RemoveRange(items);
            var error = await DbSave.TryDeleteAsync(db);
            if (error is not null)
                return (0, error);
            foreach (var row in snapshots)
                await audit.LogAsync(AuditActions.HardDelete, AuditEntities.Page, row.Id, row.Title, "Xóa vĩnh viễn", row.Details);
            return (snapshots.Count, null);
        }

        foreach (var page in items)
        {
            SoftDelete.Mark(page, username);
            var slug = page.Slug;
            SoftDelete.MarkSlugDeleted(ref slug, page.Id);
            page.Slug = slug;
            page.IsPublished = false;
        }

        await db.SaveChangesAsync();
        foreach (var page in items)
        {
            await audit.LogAsync(AuditActions.SoftDelete, AuditEntities.Page, page.Id, page.Title, "Đưa vào thùng rác",
                $"Title: {page.Title}\nSlug: {page.Slug}");
        }

        return (items.Count, null);
    }

    private static List<int> NormalizeIds(IEnumerable<int> ids) =>
        ids.Distinct().Where(id => id > 0).ToList();
}

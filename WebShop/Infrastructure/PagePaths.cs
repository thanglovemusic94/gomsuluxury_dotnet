using Microsoft.EntityFrameworkCore;
using WebShop.Data;

namespace WebShop.Infrastructure;

/// <summary>URL trang tĩnh public: /{slug} (không còn /p/).</summary>
public static class PagePaths
{
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "login", "logout", "error", "accessdenied",
        "san-pham", "blog", "gio-hang", "thanh-toan", "danh-muc", "qua-bieu", "chot", "p",
        "css", "js", "lib", "uploads", "media", "favicon.ico"
    };

    public static bool IsReserved(string? slug)
    {
        var value = NormalizeSlug(slug);
        if (string.IsNullOrEmpty(value))
            return true;
        var first = value.Split('/', 2)[0];
        return Reserved.Contains(first);
    }

    public static string NormalizeSlug(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return string.Empty;

        var value = slug.Trim().Trim('/');
        if (value.StartsWith("p/", StringComparison.OrdinalIgnoreCase))
            value = value[2..];
        return value.Trim('/');
    }

    public static string PublicUrl(string? slug)
    {
        var value = NormalizeSlug(slug);
        return string.IsNullOrEmpty(value) ? "/" : "/" + value;
    }

    /// <summary>Chuẩn hóa menu/page/block cũ từ /p/... sang /...</summary>
    public static async Task MigrateLegacyPrefixAsync(AppDbContext db)
    {
        var changed = false;

        var menus = await db.Menus.Where(menu => menu.Url.Contains("/p/")).ToListAsync();
        foreach (var menu in menus)
        {
            var next = RewritePath(menu.Url);
            if (next != menu.Url)
            {
                menu.Url = next;
                changed = true;
            }
        }

        var pages = await db.CustomPages
            .IgnoreQueryFilters()
            .Where(page => page.Slug.Contains("p/") || page.Slug.StartsWith("/"))
            .ToListAsync();
        foreach (var page in pages)
        {
            var next = NormalizeSlug(page.Slug);
            if (!string.IsNullOrEmpty(next) && next != page.Slug)
            {
                page.Slug = next;
                changed = true;
            }
        }

        var blocks = await db.HtmlBlocks.Where(block => block.Content.Contains("/p/")).ToListAsync();
        foreach (var block in blocks)
        {
            var next = block.Content
                .Replace("href=\"/p/", "href=\"/", StringComparison.OrdinalIgnoreCase)
                .Replace("href='/p/", "href='/", StringComparison.OrdinalIgnoreCase)
                .Replace("\"/p/", "\"/", StringComparison.Ordinal);
            if (next != block.Content)
            {
                block.Content = next;
                block.UpdatedAt = DateTime.UtcNow;
                changed = true;
            }
        }

        if (changed)
            await db.SaveChangesAsync();
    }

    private static string RewritePath(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;
        var trimmed = url.Trim();
        if (trimmed.StartsWith("/p/", StringComparison.OrdinalIgnoreCase))
            return "/" + trimmed[3..].TrimStart('/');
        if (trimmed.Equals("/p", StringComparison.OrdinalIgnoreCase))
            return "/";
        return trimmed;
    }
}

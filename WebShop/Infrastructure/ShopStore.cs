using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Models;

namespace WebShop.Infrastructure;

public class MenuNode
{
    public string Name { get; init; } = string.Empty;

    public string Url { get; init; } = "/";

    public List<MenuNode> Children { get; init; } = [];
}

public class CategoryNavItem
{
    public string Name { get; init; } = string.Empty;

    public string Url { get; init; } = "/";

    public string IconUrl { get; init; } = string.Empty;
}

public class CategoryBadgeItem
{
    public string Name { get; init; } = string.Empty;

    public string Url { get; init; } = "/";

    public string IconUrl { get; init; } = string.Empty;

    public bool Active { get; init; }
}

public class ShopSnapshot
{
    public IReadOnlyList<MenuNode> Header { get; init; } = [];

    public IReadOnlyList<CategoryNavItem> CategoryNav { get; init; } = [];

    public IReadOnlyList<MenuNode> Footer { get; init; } = [];

    public string SiteName { get; init; } = "WebShop";

    public string Hotline { get; init; } = string.Empty;

    public string Address { get; init; } = string.Empty;

    public string LogoUrl { get; init; } = string.Empty;

    public string FacebookUrl { get; init; } = string.Empty;

    public string TikTokUrl { get; init; } = string.Empty;

    public string ZaloUrl { get; init; } = string.Empty;

    public string HotlineAlt { get; init; } = string.Empty;

    public string BctNote { get; init; } = string.Empty;

    public int CartCount { get; init; }
}

public class ShopStore(AppDbContext db, CartService cart)
{
    private ShopSnapshot? snapshot;

    public async Task<ShopSnapshot> GetAsync()
    {
        if (snapshot is not null)
            return snapshot;

        var settings = await db.SystemSettings.AsNoTracking().ToListAsync();
        string Value(string key) => settings.FirstOrDefault(item => item.Key == key)?.Value?.Trim() ?? string.Empty;

        var menus = await db.Menus.AsNoTracking()
            .Where(menu => menu.IsActive)
            .OrderBy(menu => menu.DisplayOrder)
            .ThenBy(menu => menu.Name)
            .ToListAsync();

        var header = Build(menus, "Header");
        if (header.Count == 0)
        {
            header =
            [
                new MenuNode { Name = "Trang chủ", Url = "/" },
                new MenuNode { Name = "Sản phẩm", Url = "/san-pham" },
                new MenuNode { Name = "Blog", Url = "/blog" }
            ];
        }

        var productRoot = header.FirstOrDefault(item =>
            item.Url == "/san-pham" ||
            item.Name.Contains("sản phẩm", StringComparison.OrdinalIgnoreCase));
        var categoryNav = productRoot?.Children.Count > 0
            ? await BuildCategoryNavAsync(productRoot.Children)
            : await BuildCategoryNavFromDbAsync();

        snapshot = new ShopSnapshot
        {
            Header = header,
            CategoryNav = categoryNav,
            Footer = Build(menus, "Footer"),
            SiteName = string.IsNullOrWhiteSpace(Value("SiteName")) ? "WebShop" : Value("SiteName"),
            Hotline = Value("Hotline"),
            Address = Value("Address"),
            LogoUrl = Value("LogoUrl"),
            FacebookUrl = Value("FacebookUrl"),
            TikTokUrl = Value("TikTokUrl"),
            ZaloUrl = Value("ZaloUrl"),
            HotlineAlt = Value("HotlineAlt"),
            BctNote = Value("BctNote"),
            CartCount = cart.Count
        };
        return snapshot;
    }

    private static List<MenuNode> Build(IReadOnlyList<Menu> menus, string position)
    {
        List<MenuNode> Walk(int? parentId) => menus
            .Where(menu => menu.Position == position && menu.ParentId == parentId)
            .Select(menu => new MenuNode
            {
                Name = menu.Name,
                Url = RootUrl(menu.Url),
                Children = Walk(menu.Id)
            })
            .ToList();

        return Walk(null);
    }

    private static string RootUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "/";

        var value = url.Trim();
        if (value.StartsWith('/') || value.StartsWith('#') || value.Contains("://", StringComparison.Ordinal))
            return value;

        return "/" + value;
    }

    private async Task<List<CategoryNavItem>> BuildCategoryNavAsync(IReadOnlyList<MenuNode> children)
    {
        var slugs = children.Select(item => SlugFromCategoryUrl(item.Url)).Where(item => item is not null).Cast<string>().ToList();
        var icons = await CategoryIconsAsync(slugs);
        return children.Select(item =>
        {
            var slug = SlugFromCategoryUrl(item.Url);
            icons.TryGetValue(slug ?? "", out var icon);
            return new CategoryNavItem { Name = item.Name, Url = item.Url, IconUrl = icon ?? "" };
        }).ToList();
    }

    private async Task<List<CategoryNavItem>> BuildCategoryNavFromDbAsync()
    {
        var rows = await db.Categories.AsNoTracking()
            .Where(item => item.Type == "Product" && item.ParentId == null)
            .OrderBy(item => item.Name)
            .Select(item => new
            {
                item.Name,
                item.Slug,
                IconUrl = item.ProductCategories
                    .Where(link => link.Product.IsVisible)
                    .OrderByDescending(link => link.Product.CreatedAt)
                    .Select(link => link.Product.ImageUrl)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return rows.Select(item => new CategoryNavItem
        {
            Name = item.Name,
            Url = "/danh-muc/" + item.Slug,
            IconUrl = item.IconUrl ?? ""
        }).ToList();
    }

    private async Task<Dictionary<string, string>> CategoryIconsAsync(IReadOnlyList<string> slugs)
    {
        if (slugs.Count == 0)
            return [];

        var rows = await db.ProductCategories.AsNoTracking()
            .Where(link => link.Product.IsVisible && slugs.Contains(link.Category.Slug))
            .OrderByDescending(link => link.Product.CreatedAt)
            .Select(link => new { link.Category.Slug, link.Product.ImageUrl })
            .ToListAsync();
        return rows
            .GroupBy(item => item.Slug)
            .ToDictionary(group => group.Key, group => group.First().ImageUrl);
    }

    private static string? SlugFromCategoryUrl(string url)
    {
        const string prefix = "/danh-muc/";
        if (!url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;
        var slug = url[prefix.Length..].Trim('/');
        return slug.Length == 0 ? null : slug;
    }
}

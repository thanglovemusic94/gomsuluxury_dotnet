using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebShop.Data;
using WebShop.Infrastructure;
using WebShop.Models;

namespace WebShop.Pages.Shop;

public class CatalogFilterItem
{
    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public int Count { get; init; }

    public bool Active { get; init; }

    public string IconUrl { get; init; } = string.Empty;
}

public class CatalogModel(AppDbContext db) : PageModel
{
    public const int PageSize = 20;

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Cat { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Sort { get; set; }

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    public string Title { get; private set; } = "Sản phẩm";

    public IReadOnlyList<Product> Products { get; private set; } = [];

    public IReadOnlyList<CatalogFilterItem> Categories { get; private set; } = [];

    public IReadOnlyList<CategoryBadgeItem> CategoryBadges { get; private set; } = [];

    public int TotalCount { get; private set; }

    public int TotalPages { get; private set; }

    public int FromItem { get; private set; }

    public int ToItem { get; private set; }

    public async Task OnGetAsync()
    {
        await LoadAsync(includeFilters: true);
    }

    public async Task<IActionResult> OnGetCardsAsync()
    {
        await LoadAsync(includeFilters: false);
        if (Products.Count == 0)
            return Content(string.Empty, "text/html");
        return Partial("_ProductCardItems", Products);
    }

    public string PageUrl(int page)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(Q))
            parts.Add("q=" + Uri.EscapeDataString(Q.Trim()));
        if (!string.IsNullOrWhiteSpace(Cat))
            parts.Add("cat=" + Uri.EscapeDataString(Cat.Trim()));
        if (!string.IsNullOrWhiteSpace(Sort) && !string.Equals(Sort, "newest", StringComparison.OrdinalIgnoreCase))
            parts.Add("sort=" + Uri.EscapeDataString(Sort.Trim()));
        if (page > 1)
            parts.Add("p=" + page);
        return parts.Count == 0 ? "/san-pham" : "/san-pham?" + string.Join("&", parts);
    }

    public string CardsUrl(int page)
    {
        var url = PageUrl(page);
        return url.Contains('?', StringComparison.Ordinal)
            ? url + "&handler=Cards"
            : url + "?handler=Cards";
    }

    private async Task LoadAsync(bool includeFilters)
    {
        if (PageNumber < 1)
            PageNumber = 1;

        var categories = await db.Categories.AsNoTracking()
            .Where(item => item.Type == "Product" && item.IsVisible)
            .OrderBy(item => item.Name)
            .ToListAsync();

        var productCounts = await db.ProductCategories.AsNoTracking()
            .Where(link => link.Category.IsVisible && link.Product.IsVisible)
            .GroupBy(link => link.CategoryId)
            .Select(group => new { CategoryId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CategoryId, item => item.Count);

        var selected = categories.FirstOrDefault(item =>
            !string.IsNullOrWhiteSpace(Cat) &&
            string.Equals(item.Slug, Cat, StringComparison.OrdinalIgnoreCase));

        var categoryIds = new List<int>();
        if (selected is not null)
        {
            Collect(categories, selected.Id, categoryIds);
            Title = selected.Name;
        }
        else if (!string.IsNullOrWhiteSpace(Cat))
        {
            // Danh mục ẩn / không tồn tại: không liệt kê sản phẩm theo slug đó.
            Title = "Sản phẩm";
        }
        else if (!string.IsNullOrWhiteSpace(Q))
        {
            Title = "Tìm: " + Q.Trim();
        }

        var query = db.Products.AsNoTracking().WhereListedOnShop();
        if (!string.IsNullOrWhiteSpace(Cat) && selected is null)
            query = query.Where(_ => false);
        var keyword = Q?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(item => item.Name.Contains(keyword));
        if (categoryIds.Count > 0)
            query = query.Where(item => item.ProductCategories.Any(link => categoryIds.Contains(link.CategoryId)));

        query = (Sort?.Trim().ToLowerInvariant()) switch
        {
            "price-asc" => query.OrderBy(item => (double)(item.DiscountPrice ?? item.Price)).ThenBy(item => item.Name),
            "price-desc" => query.OrderByDescending(item => (double)(item.DiscountPrice ?? item.Price)).ThenBy(item => item.Name),
            "name" => query.OrderBy(item => item.Name),
            _ => query.OrderByDescending(item => item.CreatedAt)
        };

        TotalCount = await query.CountAsync();
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        if (PageNumber > TotalPages)
            PageNumber = TotalPages;

        Products = await query
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
        await ProductCardMedia.AttachHoverImagesAsync(db, Products);

        FromItem = TotalCount == 0 ? 0 : (PageNumber - 1) * PageSize + 1;
        ToItem = TotalCount == 0 ? 0 : FromItem + Products.Count - 1;

        if (!includeFilters)
            return;

        var iconRows = await db.ProductCategories.AsNoTracking()
            .Where(link => link.Category.IsVisible && link.Product.IsVisible && link.Product.ImageUrl != null && link.Product.ImageUrl != "")
            .OrderByDescending(link => link.Product.CreatedAt)
            .Select(link => new { link.CategoryId, link.Product.ImageUrl })
            .ToListAsync();
        var iconByCategory = iconRows
            .GroupBy(item => item.CategoryId)
            .ToDictionary(group => group.Key, group => group.First().ImageUrl ?? "");

        Categories = categories
            .Where(item => item.ParentId is null || productCounts.ContainsKey(item.Id))
            .Select(item =>
            {
                var ids = new List<int>();
                Collect(categories, item.Id, ids);
                var count = ids.Sum(id => productCounts.GetValueOrDefault(id));
                var icon = ids.Select(id => iconByCategory.GetValueOrDefault(id)).FirstOrDefault(url => !string.IsNullOrWhiteSpace(url)) ?? "";
                return new CatalogFilterItem
                {
                    Name = item.Name,
                    Slug = item.Slug,
                    Count = count,
                    Active = selected?.Id == item.Id,
                    IconUrl = icon
                };
            })
            .Where(item => item.Count > 0 || item.Active)
            .OrderBy(item => item.Name)
            .ToList();

        var badges = new List<CategoryBadgeItem>
        {
            new()
            {
                Name = "Tất cả",
                Url = "/san-pham",
                Active = string.IsNullOrWhiteSpace(Cat)
            }
        };
        badges.AddRange(Categories.Select(item => new CategoryBadgeItem
        {
            Name = item.Name,
            Url = "/san-pham?cat=" + Uri.EscapeDataString(item.Slug),
            IconUrl = item.IconUrl,
            Active = item.Active
        }));
        CategoryBadges = badges;
    }

    private static void Collect(IReadOnlyList<Category> categories, int id, List<int> ids)
    {
        ids.Add(id);
        foreach (var child in categories.Where(item => item.ParentId == id))
            Collect(categories, child.Id, ids);
    }
}
